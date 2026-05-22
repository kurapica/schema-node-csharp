using System.Text.Json;
using System.Text.Json.Serialization;
using SchemaNode.App.Schema;
using SchemaNode.Enum;
using SchemaNode.Schema;
using SchemaNode.Struct;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.App;

/// <summary>
/// The in-memory application workflow representation.
/// Populated from <see cref="AppWorkflowSchema"/> during application loading.
/// Activation / deactivation is delegated to <see cref="IWorkflowEngine"/> so the
/// workflow execution engine can be supplied externally without coupling this type to it.
/// </summary>
public sealed class AppWorkflowType : IDisposable
{
    #region Properties

    /// <summary>Application name (from owning AppType)</summary>
    public string App => Application.Name;

    /// <summary>Workflow name</summary>
    public required string Name { get; init; }

    /// <summary>Display order</summary>
    public int Seqno { get; internal set; }

    /// <summary>Display name</summary>
    public LocaleString? Display { get; private set; }

    /// <summary>Inline authentication policies</summary>
    public PolicyItem[]? Auths { get; private init; }

    /// <summary>Whether the workflow should be kept running</summary>
    public bool Active { get; internal set; }

    /// <summary>Ordered list of workflow node definitions</summary>
    public AppWorkflowNodeSchema[] Nodes { get; internal set; } = [];

    /// <summary>Properties resolved from Extensions at load time</summary>
    public object[]? Properties { get; internal set; }

    /// <summary>Extension data</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extensions { get; internal set; }

    #endregion

    #region State

    private int _activated;

    /// <summary>Whether the workflow has been activated</summary>
    public bool Activated => _activated > 0;

    /// <summary>Load / validation status — null means ready</summary>
    public string? Status { get; internal set; }

    #endregion

    #region Relationships

    /// <summary>Owning AppType</summary>
    public AppType Application { get; internal set; } = null!;

    #endregion

    #region Methods

    /// <summary>
    /// Resolves payload types for all nodes and activates the workflow if it is marked as active.
    /// Activation is performed by the supplied <paramref name="engine"/>; pass null to skip activation.
    /// </summary>
    public async Task LoadAsync(IAppSchemaContext context, IWorkflowEngine? engine = null)
    {
        foreach (AppWorkflowNodeSchema node in Nodes)
        {
            if (!string.IsNullOrWhiteSpace(node.Payload))
            {
                node.PayloadSchemaType = await context.GetNodeTypeAsync(node.Payload);
                node.PayloadSchemaType?.AddUsedBy(this);
            }
        }

        if (Nodes.Length <= 1 || !Active) return;

        if (engine != null)
            await engine.ActivateAsync(this, context);
        else
            Interlocked.CompareExchange(ref _activated, 1, 0);
    }

    /// <summary>Releases all payload type references.</summary>
    public void Release()
    {
        foreach (AppWorkflowNodeSchema node in Nodes)
            node.PayloadSchemaType?.RemoveUsedBy(this);
    }

    /// <summary>Deactivates the workflow.</summary>
    public async Task DeactivateAsync(IWorkflowEngine? engine = null)
    {
        if (Interlocked.CompareExchange(ref _activated, 0, 1) != 1) return;
        if (engine != null)
            await engine.DeactivateAsync(this);
    }

    /// <summary>
    /// Gets all effective auth policies for the given scope, inheriting from the owning application.
    /// </summary>
    public IEnumerable<PolicyItem> GetAuthPolicies(PolicyScope scope)
    {
        foreach (PolicyItem i in Application.GetAuthPolicies(scope))
            yield return i;
        if (Auths == null) yield break;
        foreach (PolicyItem i in Auths.Where(p => p.Scope == scope))
            yield return i;
    }

    public void Dispose() { /* engine holds any native resources */ }

    #endregion

    #region Conversions

    public static implicit operator AppWorkflowType(AppWorkflowSchema schema)
        => new()
        {
            Name = schema.Name,
            Seqno = schema.Seqno,
            Display = schema.Display,
            Auths = schema.Auths,
            Active = schema.Active,
            Nodes = schema.Nodes.ToArray(),
            Extensions = schema.Extensions,
        };

    public static implicit operator AppWorkflowSchema(AppWorkflowType type)
        => new()
        {
            App = type.App,
            Name = type.Name,
            Display = type.Display,
            Auths = type.Auths,
            Seqno = type.Seqno,
            Active = type.Activated,
            Nodes = type.Nodes.ToArray(),
            Extensions = type.Extensions,
        };

    #endregion
}

/// <summary>
/// Abstraction over the workflow execution engine.
/// Implement and register this in DI to enable workflow activation / deactivation.
/// </summary>
public interface IWorkflowEngine
{
    /// <summary>Activates the workflow and starts tracking its execution state.</summary>
    Task ActivateAsync(AppWorkflowType workflow, IAppSchemaContext context);

    /// <summary>Deactivates the workflow and disposes its execution context.</summary>
    Task DeactivateAsync(AppWorkflowType workflow);
}
