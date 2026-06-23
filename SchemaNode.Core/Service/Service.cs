using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Property;
using SchemaNode.Property.Record;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Utility;

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
        // load locales
        Locale.TryLoad();
        
        // system access
        services.TryAddSingleton<SystemAccess>();
        
        // Default run-time
        services.TryAddSingleton<ISchemaRuntime, SchemaRuntime>();
        
        // Default Schema context
        services.TryAddScoped<ISchemaContext, SchemaContext>();
        services.AddScoped<SchemaContext>();
        
        // The schema runtime builder
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IRuntimeStageHandler, NodeRuntimeStageHandler>());
        
        // Add logger
        services.TryAddSingleton<ILoggerFactory, LoggerFactory>();
        services.TryAddScoped(typeof(ILogger<>), typeof(Logger<>));

        #region Register Assemblys
        
        List<Assembly> orderAssemblies = [];
        Dictionary<Assembly, bool> loadingAssemblies = [];

        // Add core first
        AddAssembly(typeof(SchemaNodeExtensions).Assembly);

        // Add others but not entry
        Assembly? entry = Assembly.GetEntryAssembly();
        SchemaOptions? options = services.FirstOrDefault(x => x.ServiceType == typeof(SchemaOptions))?
            .ImplementationInstance as SchemaOptions;
        if (options?.Assemblies != null)
            foreach (Assembly assembly in options.Assemblies.Where(a => a != entry))
                AddAssembly(assembly);
        
        // Prepare the loading schema assemblies
        foreach (var assembly in assemblies.Where(a => a != entry))
            AddAssembly(assembly);
        
        // Add entry last
        if (entry != null) AddAssembly(entry);

        if (options != null)
            options.Assemblies = orderAssemblies.ToArray();
        else
            services.AddSingleton(new SchemaOptions
            {
                Assemblies = orderAssemblies.ToArray(),
            });
        
        // init with service collections
        using var provider = services.BuildServiceProvider();
        IRuntimeStageHandler[] handlers = provider.GetServices<IRuntimeStageHandler>().ToArray();
        
        // Register for services
        foreach (IRuntimeStageHandler handler in handlers)
            handler.OnServiceInitialization(provider, services, assemblies);
        
        // Done with all registered services
        foreach (IRuntimeStageHandler handler in handlers)
            handler.OnServiceInitialized(provider, services, assemblies);
        
        // Gets all stage handlers
        return services;
        
        void AddAssembly(Assembly assembly)
        {
            if (!loadingAssemblies.TryAdd(assembly, true)) return;
            orderAssemblies.Add(assembly);
        }
        
        #endregion
    }
    
    /// <summary>
    /// Add schema assembly of the given type
    /// </summary>
    public static IServiceCollection AddSchemaAssembly<T>(this IServiceCollection services) where T: class 
        => services.AddSchemaAssemblies(typeof(T).Assembly);
    
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
        Dictionary<string, Dictionary<string, Type>> schemaProperties = new();

        // Gather the schema kind & schema properties
        foreach (Assembly assembly in assemblies)
        {
            foreach (Type type in assembly.GetTypes())
            {
                // Gather [Meta<SchemaKind>] attribute
                foreach (SchemaKind asSchemaKind in type.GetMetaProperties<SchemaKind>())
                {
                    if (!schemaKinds.TryAdd(asSchemaKind.Value!, (type, asSchemaKind)))
                        throw new Exception($"Duplicate schema kind '{asSchemaKind.Value!}' found in type '{type.FullName}' and '{schemaKinds[asSchemaKind.Value!].schemaType.FullName}'");

                    foreach (Append append in type.GetMetaProperties<Append>())
                    {
                        if (append.Value is not { Length: > 0 }) continue;
                        foreach (Type propType in append.Value.Where(p => p.IsAssignableTo(typeof(IProperty))))
                        {
                            if (!schemaProperties.TryGetValue(asSchemaKind.Value!, out Dictionary<string, Type>? propertyTypes))
                            {
                                propertyTypes = new Dictionary<string, Type>();
                                schemaProperties[asSchemaKind.Value!] = propertyTypes;
                            }
                            
                            string propName = propType.GetPropertyName();
                            if (!propertyTypes.TryAdd(propName, type))
                                throw new Exception($"Duplicate property name '{propName}' found for schema kind '{asSchemaKind.Value}' in type '{type.FullName}' and '{propertyTypes[propName].FullName}'");
                        }
                    }
                }

                // Gather properties
                if (type is { IsClass: true, IsAbstract: false } && 
                    type.IsAssignableTo(typeof(IProperty)) && 
                    type.GetMetaProperty<ForSchema>() is { HasValue: true } forSchema)
                {
                    string propName = type.GetPropertyName();
                    foreach (string kind in forSchema.Value!)
                    {
                        if (!schemaProperties.TryGetValue(kind, out Dictionary<string, Type>? propertyTypes))
                        {
                            propertyTypes = new Dictionary<string, Type>();
                            schemaProperties[kind] = propertyTypes;
                        }

                        if (!propertyTypes.TryAdd(propName, type))
                            throw new Exception($"Duplicate property name '{propName}' found for schema kind '{kind}' in type '{type.FullName}' and '{propertyTypes[propName].FullName}'");
                    }
                }
            }
        }
        
        // Register the schema kinds into the runtime
        foreach (var item in schemaKinds.Values.OrderBy(t => t.kind.Order))
        {
            logger.LogSchemaKindRegistered(item.kind.Value!, item.schemaType.Name);
            runtime.RegisterSchemaKind(item.kind.Value!, item.schemaType, 
                schemaProperties.TryGetValue(item.kind.Value!, out Dictionary<string, Type>? propertyTypes) 
                    ? SortProperties(propertyTypes.Values.ToList()) 
                    : null);
        }

        // System Schema
        runtime.Stage = Enum.RuntimeStage.SystemSchemaLoading;
        await DispatchAsync("SystemSchemaLoading", h => h.OnSystemSchemaLoading(context, assemblies));

        runtime.Stage = Enum.RuntimeStage.SystemSchemaLoaded;
        await DispatchAsync("SystemSchemaLoaded",  h => h.OnSystemSchemaLoaded(context, assemblies));
        
        // Schema
        runtime.Stage = Enum.RuntimeStage.SchemaLoading;
        await DispatchAsync("SchemaLoading", h => h.OnSchemaLoadingAsync(context));

        runtime.Stage = Enum.RuntimeStage.SchemaLoaded;
        await DispatchAsync("SchemaLoaded",  h => h.OnSchemaLoadedAsync(context));
        
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
            // Maybe use relation as sort, but in backend it's not requried to sort properties, so just return as is for now
            return types.ToArray();
        }
    }

    /// <summary>
    /// Activating the schema runtime
    /// </summary>
    public static async Task<IServiceProvider> ActivateSchemaRuntimeAsync(this IServiceProvider provider)
    {
        using IServiceScope scope = provider.CreateScope();
        ISchemaRuntime runtime = scope.ServiceProvider.GetRequiredService<ISchemaRuntime>();
        ISchemaContext context = scope.ServiceProvider.GetRequiredService<ISchemaContext>();
        ILogger logger = scope.ServiceProvider.GetRequiredService<ILogger<ISchemaRuntime>>();
        IRuntimeStageHandler[] handlers = scope.ServiceProvider.GetServices<IRuntimeStageHandler>().ToArray();
        
        // Activate
        runtime.Stage = Enum.RuntimeStage.Activating;
        await DispatchAsync("Activating", h => h.OnActivatingAsync(context));

        runtime.Stage = Enum.RuntimeStage.Activated;
        await DispatchAsync("Activated",  h => h.OnActivatedAsync(context));

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
        ISchemaRuntime runtime = scope.ServiceProvider.GetRequiredService<ISchemaRuntime>();
        ILogger logger = scope.ServiceProvider.GetRequiredService<ILogger<ISchemaRuntime>>();
        IRuntimeStageHandler[] handlers = scope.ServiceProvider.GetServices<IRuntimeStageHandler>().ToArray();
        
        // Activate
        runtime.Stage = Enum.RuntimeStage.Deactivating;
        await DispatchAsync("Deactivating", h => h.OnDeactivatingAsync(context));

        runtime.Stage = Enum.RuntimeStage.Deactivated;
        await DispatchAsync("Deactivated",  h => h.OnDeactivatedAsync(context));

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