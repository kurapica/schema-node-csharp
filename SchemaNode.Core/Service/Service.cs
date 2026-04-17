using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Property;
using SchemaNode.Property.Schema;
using SchemaNode.Runtime;
using SchemaNode.Utility;
using ValueType = System.ValueType;

// ReSharper disable AccessToDisposedClosure

namespace SchemaNode.Service;

/// <summary>
/// The schema extension methods to add schema assemblies and prepare/start the schema runtime
/// </summary>
public static partial class SchemaNodeExtensions
{
    #region Fields

    private static readonly ConcurrentQueue<Assembly> OrderAssemblies = [];
    private static readonly ConcurrentDictionary<Assembly, bool> LoadingAssemblies = [];
    private static readonly ConcurrentBag<Type> StageHandlers = [];
    
    #endregion

    #region Extension methods

    /// <summary>
    /// Add schema frameworks from assemblies
    /// </summary>
    public static IServiceCollection AddSchemaAssemblies(this IServiceCollection services, params Assembly[] assemblies)
    {
        // Default run-time
        services.TryAddSingleton<ISchemaRuntime, SchemaRuntime>();
        
        // Default Schema context
        services.TryAddScoped<ISchemaContext, SchemaContext>();
        
        // The schema runtime builder
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IStageHandler, NodeSchemaRuntimeBuilder>());
        
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
        
        // Gets all stage handlers
        return services;
    }
    
    /// <summary>
    /// Add schema assembly of the given type
    /// </summary>
    public static IServiceCollection AddSchemaAssembly<T>(this IServiceCollection services) where T: class => services.AddSchemaAssemblies(typeof(T).Assembly);
    
    /// <summary>
    /// Loading the schema runtime
    /// </summary>
    public static async Task<IServiceProvider> LoadSchemaRuntimeAsync(this IServiceProvider provider)
    {
        using IServiceScope scope = provider.CreateScope();
        ISchemaRuntime runtime = scope.ServiceProvider.GetRequiredService<ISchemaRuntime>();
        ISchemaContext context = scope.ServiceProvider.GetRequiredService<ISchemaContext>();
        ILogger logger = scope.ServiceProvider.GetRequiredService<ILogger<ISchemaRuntime>>();
        
        Dictionary<Type, IStageHandler> handlers = [];
        Assembly[] assemblies = OrderAssemblies.ToArray();

        #region Schema Kind Loading

        foreach (Assembly assembly in assemblies)
        {
            Dictionary<string, (Type schemaType, AsSchemaKind kind)> map = [];
            foreach (Type type in assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false }))
            {
                // Check if this type has [Meta<AsSchemaKind>] attribute
                AsSchemaKind? asSchemaKind = type.GetMetaProperty<AsSchemaKind>();
                if (asSchemaKind is not { HasValue: true }) continue;
                map[asSchemaKind.Value!] = (type, asSchemaKind);
            }

            // Register the schema kinds
            foreach (var item in map.Values.OrderBy(t => t.kind.Order))
            {
                logger.LogSchemakindRegistered(item.kind.Value!, item.schemaType.Name);
                runtime.RegisterSchemaKind(item.kind.Value!, item.schemaType);
            }
        }

        Dispatch("PreSchemaKindLoad",   h => h.OnPreSchemaKindLoad(context, assemblies));
        Dispatch("SchemaKindLoading",   h => h.OnSchemaKindLoading(context, assemblies));
        Dispatch("SchemaKindLoaded",    h => h.OnSchemaKindLoaded(context, assemblies));

        #endregion 
        
        #region Schema Properties Loading
        
        // Property For Schemas
        Dispatch("PrePropertyLoad",     h => h.OnPrePropertyLoad(context, assemblies));
        Dispatch("PropertyLoading",     h => h.OnPropertyLoading(context, assemblies));
        Dispatch("PropertyLoaded",      h => h.OnPropertyLoaded(context, assemblies));
        
        #endregion
        
        #region System Schema Loading

        // System Schema
        Dispatch("PreSystemSchemaLoad", h => h.OnPreSystemSchemaLoad(context, assemblies));
        Dispatch("SystemSchemaLoading", h => h.OnSystemSchemaLoading(context, assemblies));
        Dispatch("SystemSchemaLoaded",  h => h.OnSystemSchemaLoaded(context, assemblies));

        #endregion
        
        #region Custom Schema Loading
        
        // Schema
        await DispatchAsync("PreSchemaLoad", h => h.OnPreSchemaLoadAsync(context));
        await DispatchAsync("SchemaLoading",  h => h.OnSchemaLoadingAsync(context));
        await DispatchAsync("SchemaLoaded",   h => h.OnSchemaLoadedAsync(context));
        
        #endregion
        
        #region Active Runtime

        // Activate
        await DispatchAsync("PreActivate",   h => h.OnPreActivateAsync(context));
        await DispatchAsync("Activating",    h => h.OnActivatingAsync(context));
        await DispatchAsync("Activated",     h => h.OnActivatedAsync(context));
        
        #endregion

        return provider;

        void Dispatch(string stage, Action<IStageHandler> invoke)
        {
            logger.LogProcessingBuildStageStage(stage);
            foreach (var handler in StageHandlers.Select(t => GetStageHandler(provider, context, handlers, t)))
                if (handler != null) invoke(handler);
        }
        
        async Task DispatchAsync(string stage, Func<IStageHandler, Task> invoke)
        {
            logger.LogProcessingRuntimeStageStage(stage);
            foreach (var handler in StageHandlers.Select(t => GetStageHandler(provider, context, handlers, t)))
                if (handler != null) await invoke(handler);
        }
    }
    
    #endregion

    #region Utility
    
    static void AddAssembly(Assembly assembly)
    {
        if (!LoadingAssemblies.TryAdd(assembly, true)) return;
        OrderAssemblies.Enqueue(assembly);

        // Register the stage handlers in the assembly
        foreach (var type in assembly.GetTypes().Where(t => 
                     typeof(IStageHandler).IsAssignableFrom(t) &&
                     t is { IsClass: true, IsAbstract: false }))
        {
            StageHandlers.Add(type);
        }
    }
    /// <summary>
    /// Gets or creates stage handler
    /// </summary>
    static IStageHandler? GetStageHandler(IServiceProvider provider, ISchemaContext context, Dictionary<Type, IStageHandler> map, Type type)
    {
        if (map.TryGetValue(type, out IStageHandler? handler)) return handler;
        handler = (IStageHandler?)ActivatorUtilities.CreateInstance(provider, type);
        if (handler != null) map.TryAdd(type, handler);
        return handler;
    }

    #endregion

    [LoggerMessage(LogLevel.Information, "Processing build stage: {Stage}")]
    static partial void LogProcessingBuildStageStage(this ILogger logger, string Stage);

    [LoggerMessage(LogLevel.Information, "Processing runtime stage: {Stage}")]
    static partial void LogProcessingRuntimeStageStage(this ILogger logger, string Stage);

    [LoggerMessage(LogLevel.Debug, "[SchemaKind] Registered kind '{Kind}' -> schema={SchemaType}")]
    static partial void LogSchemakindRegistered(this ILogger logger, string Kind, string SchemaType);
}