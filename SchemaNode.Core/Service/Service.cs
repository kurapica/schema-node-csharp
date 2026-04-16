using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchemaNode.Context;
using SchemaNode.Runtime;
// ReSharper disable AccessToDisposedClosure

namespace SchemaNode.Service;

/// <summary>
/// The schema extension methods to add schema assemblies and prepare/start the schema runtime
/// </summary>
public static class SchemaNodeExtensions
{
    #region Fields

    private static readonly ConcurrentQueue<Assembly> OrderAssemblies = [];
    private static readonly ConcurrentDictionary<Assembly, bool> LoadingAssemblies = [];
    private static readonly ConcurrentBag<Type> StageHandlers = [];
    
    static SchemaNodeExtensions()
    {
        // Add core first
        AddAssembly(typeof(SchemaNodeExtensions).Assembly);
    }
    
    #endregion

    #region Extension methods

    /// <summary>
    /// Add schema frameworks from assemblies
    /// </summary>
    public static IServiceCollection AddSchemaAssemblies(this IServiceCollection services, params Assembly[] assemblies)
    {
        Assembly? entry = Assembly.GetEntryAssembly();
        
        // Prepare the loading schema assemblies
        foreach (var assembly in assemblies)
        {
            if (assembly == entry) continue;
            AddAssembly(assembly);
        }
        
        // Gets all stage handlers
        return services;
    }
    
    /// <summary>
    /// Add schema assembly of the given type
    /// </summary>
    public static IServiceCollection AddSchemaAssembly<T>(this IServiceCollection services) where T: class => services.AddSchemaAssemblies(typeof(T).Assembly);

    /// <summary>
    /// Build schema runtime from assemblies
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection PrepareSchemaRuntime(this IServiceCollection services)
    {
        // Add core and entry assembly as default
        if (OrderAssemblies.IsEmpty) services.AddSchemaAssemblies();
        
        // Default run-time
        services.TryAddSingleton<ISchemaRunTime, SchemaRuntime>();
        
        // The context to access the run-time
        services.TryAddScoped<ISchemaContext, SchemaContext>();

        // Prepare the run-time
        using ServiceProvider provider = services.BuildServiceProvider();
        using SchemaContext context = provider.GetRequiredService<SchemaContext>();
        Dictionary<Type, IStageHandler> handlers = [];
        IEnumerable<Assembly> assemblies = OrderAssemblies.AsEnumerable();

        // Property
        Dispatch("PrePropertyLoad",     h => h.OnPrePropertyLoad(context, assemblies));
        Dispatch("PropertyLoading",     h => h.OnPropertyLoading(context, assemblies));
        Dispatch("PropertyLoaded",      h => h.OnPropertyLoaded(context, assemblies));

        // Schema Kind
        Dispatch("PreSchemaKindLoad",   h => h.OnPreSchemaKindLoad(context, assemblies));
        Dispatch("SchemaKindLoading",   h => h.OnSchemaKindLoading(context, assemblies));
        Dispatch("SchemaKindLoaded",    h => h.OnSchemaKindLoaded(context, assemblies));

        // System Schema
        Dispatch("PreSystemSchemaLoad", h => h.OnPreSystemSchemaLoad(context, assemblies));
        Dispatch("SystemSchemaLoading", h => h.OnSystemSchemaLoading(context, assemblies));
        Dispatch("SystemSchemaLoaded",  h => h.OnSystemSchemaLoaded(context, assemblies));

        return services;

        void Dispatch(string stage, Action<IStageHandler> invoke)
        {
            context.LogInformation("Processing build stage: {Stage}", stage);
            foreach (var handler in StageHandlers.Select(t => GetStageHandler(context, handlers, t)))
                if (handler != null) invoke(handler);
        }
    }
    
    public static async Task<IServiceProvider> StartSchemaRuntimeAsync(this IServiceProvider provider)
    {
        using IServiceScope scope = provider.CreateScope();
        using SchemaContext context = scope.ServiceProvider.GetRequiredService<SchemaContext>();
        Dictionary<Type, IStageHandler> handlers = [];

        // Schema
        await DispatchAsync("PreSchemaLoad", h => h.OnPreSchemaLoadAsync(context));
        await DispatchAsync("SchemaLoading",  h => h.OnSchemaLoadingAsync(context));
        await DispatchAsync("SchemaLoaded",   h => h.OnSchemaLoadedAsync(context));

        // Activate
        await DispatchAsync("PreActivate",   h => h.OnPreActivateAsync(context));
        await DispatchAsync("Activating",    h => h.OnActivatingAsync(context));
        await DispatchAsync("Activated",     h => h.OnActivatedAsync(context));

        return provider;

        async Task DispatchAsync(string stage, Func<IStageHandler, Task> invoke)
        {
            context.LogInformation("Processing runtime stage: {Stage}", stage);
            foreach (var handler in StageHandlers.Select(t => GetStageHandler(context, handlers, t)))
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
    static IStageHandler? GetStageHandler(SchemaContext context, Dictionary<Type, IStageHandler> map, Type type)
    {
        if (map.TryGetValue(type, out IStageHandler? handler)) return handler;
        handler = (IStageHandler?)ActivatorUtilities.CreateInstance(context.ServiceProvider, type);
        if (handler != null) map.TryAdd(type, handler);
        return handler;
    }

    #endregion
}