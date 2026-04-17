using System.Reflection;
using SchemaNode.Context;

namespace SchemaNode.Service;

/// <summary>
/// The schema loading stage handler. Each virtual method corresponds to a fixed stage in the loading pipeline.
/// Implement only the stages you need to hook into.
/// </summary>
public interface IStageHandler
{
    #region Build stages (synchronous, called during PrepareSchemaRuntime)

    /// <summary>Called before property loading begins.</summary>
    public void OnPrePropertyLoad(ISchemaContext context, IEnumerable<Assembly> assemblies) { }

    /// <summary>Called while properties are being loaded.</summary>
    public void OnPropertyLoading(ISchemaContext context, IEnumerable<Assembly> assemblies) { }

    /// <summary>Called after all properties have been loaded.</summary>
    public void OnPropertyLoaded(ISchemaContext context, IEnumerable<Assembly> assemblies) { }

    /// <summary>Called before schema-kind loading begins.</summary>
    public void OnPreSchemaKindLoad(ISchemaContext context, IEnumerable<Assembly> assemblies) { }

    /// <summary>Called while schema kinds are being loaded.</summary>
    public void OnSchemaKindLoading(ISchemaContext context, IEnumerable<Assembly> assemblies) { }

    /// <summary>Called after all schema kinds have been loaded.</summary>
    public void OnSchemaKindLoaded(ISchemaContext context, IEnumerable<Assembly> assemblies) { }

    /// <summary>Called before system-schema loading begins.</summary>
    public void OnPreSystemSchemaLoad(ISchemaContext context, IEnumerable<Assembly> assemblies) { }

    /// <summary>Called while system schemas are being loaded.</summary>
    public void OnSystemSchemaLoading(ISchemaContext context, IEnumerable<Assembly> assemblies) { }

    /// <summary>Called after all system schemas have been loaded.</summary>
    public void OnSystemSchemaLoaded(ISchemaContext context, IEnumerable<Assembly> assemblies) { }

    #endregion

    #region Runtime stages (asynchronous, called during StartSchemaRuntimeAsync)

    /// <summary>Called before custom schema loading begins.</summary>
    public Task OnPreSchemaLoadAsync(ISchemaContext context) => Task.CompletedTask;

    /// <summary>Called while custom schemas are being loaded.</summary>
    public Task OnSchemaLoadingAsync(ISchemaContext context) => Task.CompletedTask;

    /// <summary>Called after all custom schemas have been loaded.</summary>
    public Task OnSchemaLoadedAsync(ISchemaContext context) => Task.CompletedTask;

    /// <summary>Called before runtime activation (all schemas are ready).</summary>
    public Task OnPreActivateAsync(ISchemaContext context) => Task.CompletedTask;

    /// <summary>Called while the runtime is being activated (apps, workflows, services).</summary>
    public Task OnActivatingAsync(ISchemaContext context) => Task.CompletedTask;

    /// <summary>Called after the runtime has been fully activated.</summary>
    public Task OnActivatedAsync(ISchemaContext context) => Task.CompletedTask;

    #endregion
}