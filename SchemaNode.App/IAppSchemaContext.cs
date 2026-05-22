using System.Text.Json.Nodes;
using SchemaNode.App.Schema;

namespace SchemaNode.App;

/// <summary>
/// App-layer schema context abstraction.
/// Separates the App runtime loading logic from the Core <c>SchemaContext</c> implementation
/// so <c>AppType</c> does not need a direct reference to Core internals.
/// </summary>
public interface IAppSchemaContext
{
    /// <summary>Resolves a node type by schema name (delegates to Core SchemaContext).</summary>
    Task<Runtime.NodeType?> GetNodeTypeAsync(string schemaName);

    /// <summary>Resolves an AppType by name. Creates it if it does not yet exist.</summary>
    Task<AppType?> GetAppTypeAsync(string appName, bool reload = false, bool preload = false);

    /// <summary>Loads the raw AppSchema for the given application name.</summary>
    Task<AppSchema?> LoadAppSchemaAsync(string appName);

    /// <summary>
    /// Calls a named function schema and returns the JSON result.
    /// Used by the policy authorization layer.
    /// </summary>
    Task<JsonNode?> CallFunctionAsync(string functionName, JsonArray args);

    /// <summary>Logs a warning message.</summary>
    void LogWarning(string message, params object[] args);

    /// <summary>Logs an error message with an exception.</summary>
    void LogError(Exception ex, string message, params object[] args);
}
