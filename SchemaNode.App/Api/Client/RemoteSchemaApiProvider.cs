using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SchemaNode.Api.Schema.Info;
using SchemaNode.Enum;
using SchemaNode.Schema;

namespace SchemaNode.Api.Client;

/// <summary>
/// Remote Schema API provider that accesses the server through HttpClient.
///
/// Request endpoints and payloads align with the TypeScript provider in schema-node-app / schema-node-man:
///   - <c>/get-schema</c>       { names, includeRefs }              → { schemas }
///   - <c>/get-app-schema</c>   { name, includeTypes, format }      → { schema }
///   - <c>/call-function</c>    { name, args, return, applyMode, app, target } → { result }
///   - <c>/save-schema</c>      { schema }                          → { result }
///   - <c>/delete-schema</c>    { name }                            → { result }
///   - <c>/save-app-schema</c>  { schema }                          → { result }
///   - <c>/delete-app-schema</c>{ app }                             → { result }
///
/// Error semantics: non-success HTTP statuses are thrown directly (matching axios throwing in TS);
/// when response fields are missing, they fall back to an empty array / null / false (matching TS's
/// `?.schemas || []` and similar patterns).
/// Serialization uses System.Text.Json + CamelCase, consistent with Core's DefaultJsonOptions.
///
/// This implementation is stateless and can be shared by multiple screens, so the App host holds it as a singleton.
/// </summary>
public sealed class RemoteSchemaApiProvider : ISchemaApiProvider
{
    // Aligned with Core Utility.Extension.DefaultJsonOptions: CamelCase + ignore null
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly string _baseAddress;

    /// <summary>
    /// Constructor. BaseAddress comes from <see cref="SchemaApiProviderOptions"/> and is not hard-coded.
    /// </summary>
    public RemoteSchemaApiProvider(HttpClient http, SchemaApiProviderOptions options)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        if (string.IsNullOrWhiteSpace(options?.BaseAddress))
            throw new ArgumentException("SchemaApiProviderOptions.BaseAddress 不能为空", nameof(options));
        _baseAddress = options.BaseAddress.TrimEnd('/');
    }

    /// <inheritdoc />
    public async Task<NodeSchema[]> GetSchemaAsync(string[] names, bool includeRefs = false, CancellationToken ct = default)
    {
        var resp = await PostAsync<GetSchemaResponse>("/get-schema", new { names, includeRefs }, ct).ConfigureAwait(false);
        return resp?.Schemas ?? [];
    }

    /// <inheritdoc />
    public async Task<JsonNode?> CallFunctionAsync(
        string schemaName,
        JsonArray? args = null,
        string? retType = null,
        ApplyMode applyMode = ApplyMode.Call,
        string? app = null,
        string? target = null,
        CancellationToken ct = default)
    {
        args ??= new JsonArray();
        var payload = new
        {
            name = schemaName,
            args,
            @return = retType,
            applyMode,
            app,
            target,
        };
        var resp = await PostAsync<CallFunctionResponse>("/call-function", payload, ct).ConfigureAwait(false);
        return resp?.Result;
    }

    /// <inheritdoc />
    public async Task<AppSchema?> GetAppSchemaAsync(
        string app,
        bool includeTypes = false,
        string? format = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(app)) return null;

        // Use the server-side request/response contracts directly so the payload keeps
        // tracking GetAppSchemaApi (name / includeTypes / format -> schema).
        var request = new GetAppSchemaRequest
        {
            Name = app,
            IncludeTypes = includeTypes,
            Format = format,
        };
        var resp = await PostAsync<GetAppSchemaResponse>("/get-app-schema", request, ct).ConfigureAwait(false);
        return resp?.Schema;
    }

    /// <inheritdoc />
    public Task<bool> SaveSchemaAsync(NodeSchema schema, CancellationToken ct = default)
        => PostBoolAsync("/save-schema", new { schema }, ct);

    /// <inheritdoc />
    public Task<bool> DeleteSchemaAsync(string name, CancellationToken ct = default)
        => PostBoolAsync("/delete-schema", new { name }, ct);

    /// <inheritdoc />
    public Task<bool> SaveAppSchemaAsync(AppSchema app, CancellationToken ct = default)
        => PostBoolAsync("/save-app-schema", new { schema = app }, ct);

    /// <inheritdoc />
    public Task<bool> DeleteAppSchemaAsync(string app, CancellationToken ct = default)
        => PostBoolAsync("/delete-app-schema", new { app }, ct);

    /// <inheritdoc />
    public Task<bool> SaveAppWorkflowSchemaAsync(string app, AppWorkflowSchema workflow, CancellationToken ct = default)
        => PostBoolAsync("/save-app-workflow-schema", new { app, schema = workflow }, ct);

    /// <inheritdoc />
    public Task<bool> DeleteAppWorkflowSchemaAsync(string app, string workflow, CancellationToken ct = default)
        => PostBoolAsync("/delete-app-workflow-schema", new { app, workflow }, ct);

    #region Helpers

    private async Task<bool> PostBoolAsync(string path, object payload, CancellationToken ct)
    {
        var resp = await PostAsync<ResultResponse>(path, payload, ct).ConfigureAwait(false);
        return resp?.Result ?? false;
    }

    private async Task<T?> PostAsync<T>(string path, object payload, CancellationToken ct) where T : class
    {
        string url = $"{_baseAddress}/{path.TrimStart('/')}";
        using var response = await _http.PostAsJsonAsync(url, payload, JsonOptions, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct).ConfigureAwait(false);
    }

    // Response contracts mirror the server SchemaApiResponse subclasses (CamelCase).
    private sealed class GetSchemaResponse { public NodeSchema[]? Schemas { get; set; } }
    private sealed class CallFunctionResponse { public JsonNode? Result { get; set; } }
    private sealed class ResultResponse { public bool Result { get; set; } }

    #endregion
}
