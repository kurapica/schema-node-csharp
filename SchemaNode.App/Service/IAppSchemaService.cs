using SchemaNode.App.Schema;
using SchemaNode.Context;
using SchemaNode.Service;

namespace SchemaNode.App;

/// <summary>
/// External provider interface for loading application schemas.
/// Register one or more implementations in DI; the context will merge results in registration order.
/// </summary>
public interface IAppSchemaProvider
{
    /// <summary>
    /// Loads the application schema for the given name.
    /// Return null if this provider does not know about the application.
    /// </summary>
    Task<AppSchema?> LoadAsync(string appName);

    /// <summary>
    /// Loads schemas for all direct sub-applications of the given parent application name.
    /// Return an empty array if none.
    /// </summary>
    Task<AppSchema[]> LoadSubAppsAsync(string parentAppName);
}

/// <summary>
/// App-layer type registry — manages AppType instances for a running context.
/// Implement and register in DI; the default implementation is <c>AppTypeManager</c>.
/// </summary>
public interface IAppTypeManager
{
    /// <summary>Gets (or creates) an AppType by name.</summary>
    Task<AppType?> GetAsync(string appName, bool reload = false, bool preload = false);

    /// <summary>Removes an AppType from the cache (e.g. after schema deletion).</summary>
    bool Remove(string appName);

    /// <summary>Pre-loads all known App types (called during runtime activation).</summary>
    Task PreloadAsync(ISchemaContext context) => Task.CompletedTask;

    /// <summary>Deactivates all managed App types (called during runtime deactivation).</summary>
    Task DeactivateAsync(ISchemaContext context) => Task.CompletedTask;
}
