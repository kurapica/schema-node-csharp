using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Property;
using SchemaNode.Property.Schema;
using SchemaNode.Runtime;

// ReSharper disable AccessToDisposedClosure

namespace SchemaNode.Service;

/// <summary>
/// The schema extension methods to add schema assemblies and prepare/start the schema runtime
/// </summary>
public static partial class SchemaNodeExtensions
{
    #region Extension methods

    /// <summary>
    /// Add schema frameworks from assemblies
    /// </summary>
    public static IServiceCollection AddSchemaAssemblies(this IServiceCollection services, params Assembly[] assemblies)
    {
        List<Assembly> orderAssemblies = [];
        Dictionary<Assembly, bool> loadingAssemblies = [];

        // Default run-time
        services.TryAddSingleton<ISchemaRuntime, SchemaRuntime>();
        
        // Default Schema context
        services.TryAddScoped<ISchemaContext, SchemaContext>();
        
        // The schema runtime builder
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IRuntimeStageHandler, NodeRuntimeStageHandler>());
        
        // Add logger
        services.TryAddSingleton<ILoggerFactory, LoggerFactory>();
        services.TryAddScoped(typeof(ILogger<>), typeof(Logger<>));

        Assembly? entry = Assembly.GetEntryAssembly();
        
        // Add core first
        AddAssembly(typeof(SchemaNodeExtensions).Assembly);
        
        // Prepare the loading schema assemblies
        foreach (var assembly in assemblies)
        {
            if (assembly == entry) continue;
            AddAssembly(assembly);
        }
        
        // Add entry last
        if (entry != null) AddAssembly(entry);

        services.AddSingleton(new SchemaOptions
        {
            Assemblies = orderAssemblies.ToArray(),
        });
        
        // Gets all stage handlers
        return services;
        
        void AddAssembly(Assembly assembly)
        {
            if (!loadingAssemblies.TryAdd(assembly, true)) return;
            orderAssemblies.Add(assembly);
        }

    }
    
    /// <summary>
    /// Add schema assembly of the given type
    /// </summary>
    public static IServiceCollection AddSchemaAssembly<T>(this IServiceCollection services) where T: class => services.AddSchemaAssemblies(typeof(T).Assembly);
    
    /// <summary>
    /// Loading the schema runtime
    /// </summary>
    public static async Task<IServiceProvider> InitSchemaRuntimeAsync(this IServiceProvider provider)
    {
        using IServiceScope scope = provider.CreateScope();
        ISchemaRuntime runtime = scope.ServiceProvider.GetRequiredService<ISchemaRuntime>();
        ISchemaContext context = scope.ServiceProvider.GetRequiredService<ISchemaContext>();
        ILogger logger = scope.ServiceProvider.GetRequiredService<ILogger<ISchemaRuntime>>();
        IRuntimeStageHandler[] handlers = scope.ServiceProvider.GetServices<IRuntimeStageHandler>().ToArray();
        Assembly[] assemblies = provider.GetService<SchemaOptions>()?.Assemblies ?? [];
        Dictionary<string, (Type schemaType, SchemaKind kind)> schemaKinds = [];
        Dictionary<string, List<Type>> schemaProperties = new();

        // Gather the schema kind & schema properties
        foreach (Assembly assembly in assemblies)
        {
            foreach (Type type in assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false }))
            {
                // Check if this type has [Meta<AsSchemaKind>] attribute
                if (type.GetMetaProperty<SchemaKind>() is { HasValue: true } asSchemaKind)
                {
                    if (schemaKinds.TryAdd(asSchemaKind.Value!, (type, asSchemaKind))) continue;
                    throw new Exception($"Duplicate schema kind '{asSchemaKind.Value!}' found in type '{type.FullName}' and '{schemaKinds[asSchemaKind.Value!].schemaType.FullName}'");
                }
                else if (type.IsAssignableTo(typeof(IProperty)) && type.GetMetaProperty<ForSchema>() is { HasValue: true } forSchema)
                {
                    foreach (string kind in forSchema.Value!)
                    {
                        if (schemaProperties.TryGetValue(kind, out List<Type>? propertyTypes))
                            propertyTypes.Add(type);
                        else
                            schemaProperties.Add(kind, [type]);
                    }
                }
            }
        }
        
        // Register the schema kinds into the runtime
        foreach (var item in schemaKinds.Values.OrderBy(t => t.kind.Order))
        {
            logger.LogSchemaKindRegistered(item.kind.Value!, item.schemaType.Name);
            runtime.RegisterSchemaKind(item.kind.Value!, item.schemaType, schemaProperties.TryGetValue(item.kind.Value!, out List<Type>? propertyTypes) ? SortProperties(propertyTypes) : null);
        }
        
        // System Schema
        await DispatchAsync("SystemSchemaLoading", h => h.OnSystemSchemaLoading(context, assemblies));
        await DispatchAsync("SystemSchemaLoaded",  h => h.OnSystemSchemaLoaded(context, assemblies));
        
        // Schema
        await DispatchAsync("SchemaLoading",  h => h.OnSchemaLoadingAsync(context));
        await DispatchAsync("SchemaLoaded",   h => h.OnSchemaLoadedAsync(context));
        
        return provider;
        
        async Task DispatchAsync(string stage, Func<IRuntimeStageHandler, Task> invoke)
        {
            logger.LogProcessingRuntimeStageStage(stage);
            foreach (var handler in handlers)
                await invoke(handler);
        }

        // sort property types with depends & option depends
        Type[] SortProperties(List<Type> types)
        {
            List<Type> sorted = [];

            foreach (Type type in types)
                InsertPropertyType(type);

            bool InsertPropertyType(Type type)
            {
                if (sorted.Contains(type)) return true;
                
                Type[]? depends = type.GetMetaProperty<Depends>()?.GetValue<Depends>()?.GetValue<Type[]>();
                Type[]? optionDepends = type.GetMetaProperty<OptionDepends>()?.GetValue<OptionDepends>()?.GetValue<Type[]>();

                if (depends is { Length: > 0 })
                {
                    foreach (Type depend in depends)
                    {
                        if (types.Contains(depend) && InsertPropertyType(depend)) continue;
                        logger.LogError("Failed to insert property type '{type}' due to missing or circular dependency on '{depend}'", type.FullName, depend.FullName);
                        return false;
                    }
                }

                if (optionDepends is  { Length: > 0 })
                {
                    foreach (Type option in optionDepends)
                    {
                        if (types.Contains(option))
                            InsertPropertyType(option);
                    }
                }
                
                sorted.Add(type);
                return true;
            }
            
            return sorted.ToArray();
        }
    }

    /// <summary>
    /// Activating the schema runtime
    /// </summary>
    public static async Task<IServiceProvider> ActivateSchemaRuntimeAsync(this IServiceProvider provider)
    {
        using IServiceScope scope = provider.CreateScope();
        ISchemaContext context = scope.ServiceProvider.GetRequiredService<ISchemaContext>();
        ILogger logger = scope.ServiceProvider.GetRequiredService<ILogger<ISchemaRuntime>>();
        IRuntimeStageHandler[] handlers = scope.ServiceProvider.GetServices<IRuntimeStageHandler>().ToArray();
        
        // Activate
        await DispatchAsync("Activating",    h => h.OnActivatingAsync(context));
        await DispatchAsync("Activated",    h => h.OnActivatedAsync(context));

        return provider;
        
        async Task DispatchAsync(string stage, Func<IRuntimeStageHandler, Task> invoke)
        {
            logger.LogProcessingRuntimeStageStage(stage);
            foreach (var handler in handlers)
                await invoke(handler);
        }
    }

    /// <summary>
    /// Deactivate the schema runtime
    /// </summary>
    public static async Task<IServiceProvider> DeactivateSchemaRuntimeAsync(this IServiceProvider provider)
    {
        using IServiceScope scope = provider.CreateScope();
        ISchemaContext context = scope.ServiceProvider.GetRequiredService<ISchemaContext>();
        ILogger logger = scope.ServiceProvider.GetRequiredService<ILogger<ISchemaRuntime>>();
        IRuntimeStageHandler[] handlers = scope.ServiceProvider.GetServices<IRuntimeStageHandler>().ToArray();
        
        // Activate
        await DispatchAsync("Deactivating",    h => h.OnDeactivatingAsync(context));
        await DispatchAsync("Deactivated",    h => h.OnDeactivatedAsync(context));

        return provider;
        
        async Task DispatchAsync(string stage, Func<IRuntimeStageHandler, Task> invoke)
        {
            logger.LogProcessingRuntimeStageStage(stage);
            foreach (var handler in handlers)
                await invoke(handler);
        }
    }
    
    #endregion

    #region Utility
    
    [LoggerMessage(LogLevel.Information, "Processing build stage: {stage}")]
    static partial void LogProcessingBuildStageStage(this ILogger logger, string stage);

    [LoggerMessage(LogLevel.Information, "Processing runtime stage: {stage}")]
    static partial void LogProcessingRuntimeStageStage(this ILogger logger, string stage);

    [LoggerMessage(LogLevel.Debug, "[SchemaKind] Registered kind '{kind}' -> schema={schemaType}")]
    static partial void LogSchemaKindRegistered(this ILogger logger, string kind, string schemaType);
    
    #endregion

    #region Inner Types

    /// <summary>
    /// The schema options
    /// </summary>
    class SchemaOptions
    {
        public Assembly[]? Assemblies { get; set; }
    }

    #endregion
}