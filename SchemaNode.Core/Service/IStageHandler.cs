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
    public void OnPrePropertyLoad(SchemaContext context, IEnumerable<Assembly> assemblies) { }

    /// <summary>Called while properties are being loaded.</summary>
    public void OnPropertyLoading(SchemaContext context, IEnumerable<Assembly> assemblies) { }

    /// <summary>Called after all properties have been loaded.</summary>
    public void OnPropertyLoaded(SchemaContext context, IEnumerable<Assembly> assemblies) { }

    /// <summary>Called before schema-kind loading begins.</summary>
    public void OnPreSchemaKindLoad(SchemaContext context, IEnumerable<Assembly> assemblies) { }

    /// <summary>Called while schema kinds are being loaded.</summary>
    public void OnSchemaKindLoading(SchemaContext context, IEnumerable<Assembly> assemblies) { }

    /// <summary>Called after all schema kinds have been loaded.</summary>
    public void OnSchemaKindLoaded(SchemaContext context, IEnumerable<Assembly> assemblies) { }

    /// <summary>Called before system-schema loading begins.</summary>
    public void OnPreSystemSchemaLoad(SchemaContext context, IEnumerable<Assembly> assemblies) { }

    /// <summary>Called while system schemas are being loaded.</summary>
    public void OnSystemSchemaLoading(SchemaContext context, IEnumerable<Assembly> assemblies) { }

    /// <summary>Called after all system schemas have been loaded.</summary>
    public void OnSystemSchemaLoaded(SchemaContext context, IEnumerable<Assembly> assemblies) { }

    #endregion

    #region Runtime stages (asynchronous, called during StartSchemaRuntimeAsync)

    /// <summary>Called before custom schema loading begins.</summary>
    public Task OnPreSchemaLoadAsync(SchemaContext context) => Task.CompletedTask;

    /// <summary>Called while custom schemas are being loaded.</summary>
    public Task OnSchemaLoadingAsync(SchemaContext context) => Task.CompletedTask;

    /// <summary>Called after all custom schemas have been loaded.</summary>
    public Task OnSchemaLoadedAsync(SchemaContext context) => Task.CompletedTask;

    /// <summary>Called before runtime activation (all schemas are ready).</summary>
    public Task OnPreActivateAsync(SchemaContext context) => Task.CompletedTask;

    /// <summary>Called while the runtime is being activated (apps, workflows, services).</summary>
    public Task OnActivatingAsync(SchemaContext context) => Task.CompletedTask;

    /// <summary>Called after the runtime has been fully activated.</summary>
    public Task OnActivatedAsync(SchemaContext context) => Task.CompletedTask;

    #endregion
}