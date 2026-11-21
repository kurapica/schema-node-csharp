namespace SchemaNode.Api.Client;

/// <summary>
/// Configuration options for the remote Schema API provider.
/// BaseAddress must contain the root path of the Schema API (e.g. http://localhost:5000/schema);
/// the exact value is determined by the server's UseSchemaApis prefix, whose default is "schema".
/// The host is never hard-coded in code and relies entirely on external configuration injection.
/// </summary>
public sealed class SchemaApiProviderOptions
{
    /// <summary>
    /// Schema API root address, e.g. http://localhost:5000/schema
    /// </summary>
    public string? BaseAddress { get; set; }
}
