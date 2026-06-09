using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Context;

namespace SchemaNode.Service;

/// <summary>
/// The schema runtime stage builder. Each virtual method corresponds to a fixed stage in the loading pipeline.
/// Implement only the stages you need to hook into.
/// </summary>
public interface IRuntimeStageHandler
{
    /// <summary> Register service to the service collections </summary>
    public void OnServiceInitialization(IServiceProvider provider, IServiceCollection services) {}
    
    /// <summary> Handles service collections when all initialized</summary>
    public void OnServiceInitialized(IServiceProvider provider, IServiceCollection services) {}
    
    /// <summary>Called while system schemas are being loaded.</summary>
    public Task OnSystemSchemaLoading(ISchemaContext context, IEnumerable<Assembly> assemblies)=> Task.CompletedTask;

    /// <summary>Called after all system schemas have been loaded.</summary>
    public Task OnSystemSchemaLoaded(ISchemaContext context, IEnumerable<Assembly> assemblies)=> Task.CompletedTask;

    /// <summary>Called while custom schemas are being loaded.</summary>
    public Task OnSchemaLoadingAsync(ISchemaContext context) => Task.CompletedTask;

    /// <summary>Called after all custom schemas have been loaded.</summary>
    public Task OnSchemaLoadedAsync(ISchemaContext context) => Task.CompletedTask;

    /// <summary>Called while the runtime is being activated (apps, workflows, services).</summary>
    public Task OnActivatingAsync(ISchemaContext context) => Task.CompletedTask;
    
    /// <summary>Called while the runtime has been activated (apps, workflows, services).</summary>
    public Task OnActivatedAsync(ISchemaContext context) => Task.CompletedTask;
    
    /// <summary>Called while the runtime is being deactivated (apps, workflows, services).</summary>
    public Task OnDeactivatingAsync(ISchemaContext context) => Task.CompletedTask;
    
    /// <summary>Called while the runtime has been deactivated (apps, workflows, services).</summary>
    public Task OnDeactivatedAsync(ISchemaContext context) => Task.CompletedTask;
}