using System.Text.Json.Nodes;
using SchemaNode.Enum;
using SchemaNode.Schema;

namespace SchemaNode.Api.Client;

/// <summary>
/// Schema data access abstraction, aligned with the schema-node-app client contract:
///   - Read side: <c>INodeSchemaProvider.getSchema</c> / <c>callFunction</c>
///   - App side: <c>IAppSchemaProvider.getAppSchema(app, includeTypes, format)</c>
///   - Write side: schema-node-man's <c>ISchemaServerProvder</c> (save/delete schema, save/delete app schema)
///
/// Differences from the TypeScript contract (constrained by the actual C# capabilities):
///   1. In TS, <c>callFunction</c> walks up from <c>source: IValueAccess</c> to find <c>AppNode</c> and derives
///      app/target; the C# side has no <c>AppNode</c> implementation, and the server-side
///      <c>CallFunctionRequest</c> explicitly uses <c>App</c> / <c>Target</c> strings, so the explicit
///      <c>app</c> / <c>target</c> parameters are kept here (consistent with the server contract rather than
///      introducing a separate one).
///   2. TS's axios calls have no explicit cancellation semantics; <see cref="CancellationToken"/> is an
///      additional capability of the C# transport layer and does not change the request/response model.
/// Error semantics match TS: transport failures are thrown directly; missing fields fall back to an empty
/// array / null / false following the <c>?.schemas || []</c>, <c>?.schema</c>, <c>?.result</c> conventions.
/// </summary>
public interface ISchemaApiProvider
{
    /// <summary>
    /// Loads type definitions in bulk by full name (corresponds to <c>getSchema(names, includeRefs)</c>).
    /// </summary>
    /// <param name="names">Collection of type full names, e.g. ["system.string", "my.ns.User"]</param>
    /// <param name="includeRefs">Whether referenced types are returned as well (the local implementation always resolves references; see the implementation notes)</param>
    /// <param name="ct">Cancellation token</param>
    Task<NodeSchema[]> GetSchemaAsync(string[] names, bool includeRefs = false, CancellationToken ct = default);

    /// <summary>
    /// Calls a Schema function (corresponds to <c>callFunction(schemaName, args, retType, applyMode, source)</c>).
    /// C# has no AppNode, so app/target are provided explicitly by the caller; see the interface description.
    /// </summary>
    /// <param name="schemaName">Function full name</param>
    /// <param name="args">Function arguments, a Json array; treated as an empty array when null</param>
    /// <param name="retType">Full name of the generic return type (TS: retType)</param>
    /// <param name="applyMode">Invocation mode (TS: applyMode)</param>
    /// <param name="app">Full name of the associated app; may be null</param>
    /// <param name="target">Full name of the associated target data; may be null</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Json node of the function return value</returns>
    Task<JsonNode?> CallFunctionAsync(
        string schemaName,
        JsonArray? args = null,
        string? retType = null,
        ApplyMode applyMode = ApplyMode.Call,
        string? app = null,
        string? target = null,
        CancellationToken ct = default);

    /// <summary>
    /// Loads an app definition by full name (corresponds to <c>getAppSchema(app, includeTypes, format)</c>).
    /// </summary>
    Task<AppSchema?> GetAppSchemaAsync(
        string app,
        bool includeTypes = false,
        string? format = null,
        CancellationToken ct = default);

    /// <summary>
    /// Saves (inserts or updates) a type definition (corresponds to schemaServerProvider.saveSchema).
    /// </summary>
    /// <returns>Whether the save succeeded</returns>
    Task<bool> SaveSchemaAsync(NodeSchema schema, CancellationToken ct = default);

    /// <summary>
    /// Deletes a type definition by full name (corresponds to schemaServerProvider.deleteSchema).
    /// </summary>
    Task<bool> DeleteSchemaAsync(string name, CancellationToken ct = default);

    /// <summary>
    /// Saves an app definition (corresponds to schemaServerProvider.saveAppSchema).
    /// </summary>
    Task<bool> SaveAppSchemaAsync(AppSchema app, CancellationToken ct = default);

    /// <summary>
    /// Deletes an app definition (corresponds to schemaServerProvider.deleteAppSchema).
    /// </summary>
    Task<bool> DeleteAppSchemaAsync(string app, CancellationToken ct = default);

    /// <summary>
    /// Saves an app workflow (corresponds to schemaServerProvider.saveAppWorkflowSchema).
    /// </summary>
    Task<bool> SaveAppWorkflowSchemaAsync(string app, AppWorkflowSchema workflow, CancellationToken ct = default);

    /// <summary>
    /// Deletes an app workflow (corresponds to schemaServerProvider.deleteAppWorkflowSchema).
    /// </summary>
    Task<bool> DeleteAppWorkflowSchemaAsync(string app, string workflow, CancellationToken ct = default);
}
