using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using SchemaNode.Enum;
using SchemaNode.Http;
using SchemaNode.Runtime;
using SchemaNode.Utility;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Api.Schema.Info;

/// <summary>
/// The Diagnose api
/// </summary>
public class DiagnoseApi : SchemaApi<DiagnoseRequest, DiagnoseResponse>
{
    /// <inheritdoc />
    protected override async Task<DiagnoseResponse?> ExecuteAsync(DiagnoseRequest request,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("[Api]Diagnose [Request]{request}", request);

        await Task.Yield();

        return new DiagnoseResponse
        {
            Namespace = DiagnoseNamespaceAsync(await SchemaContext.GetSchemaTypeAsync("")),
            App = DiagnoseAppAsync(await SchemaContext.GetAppTypeAsync("")),
        };
    }

    private static JsonNode? DiagnoseNamespaceAsync(AnySchemaType? schema)
    {
        switch (schema)
        {
            case null:
                return null;
            case TypeNamespace ns:
                JsonObject? res = null;
                foreach ((_, AnySchemaType s) in ns.SchemaNodes)
                {
                    var r = DiagnoseNamespaceAsync(s);
                    if (r == null || r.IsEmpty()) continue;
                    res ??= new JsonObject();
                    res[s.Name] = r;
                }

                return res;
            default:
                return schema.Status != SchemaNodeStatus.Ready ? schema.Status.ToString() : null;
        }
    }

    private static JsonNode? DiagnoseAppAsync(AppType? app)
    {
        if (app == null) return null;
        
        if (app.Fields is { Count: > 0 }) return app.Status != SchemaNodeStatus.Ready ? app.Status.ToString() : null;
        if (app.SubAppList == null || app.SubAppList.IsEmpty) return null;
        
        JsonObject? res = null;
        foreach ((_, AppType a) in app.SubAppList)
        {
            var r = DiagnoseAppAsync(a);
            if (r == null || r.IsEmpty()) continue;
            res ??= new JsonObject();
            res[a.Name] = r;
        }
        return res;
    }
}

/// <summary>
/// The Diagnose request
/// </summary>
public class DiagnoseRequest : SchemaApiRequest
{
}

/// <summary>
/// The Diagnose response
/// </summary>
public class DiagnoseResponse : SchemaApiResponse
{
    /// <summary>
    /// The namespace diagnose result
    /// </summary>
    public JsonNode? Namespace { get; set; }
    
    /// <summary>
    /// The app diagnose result
    /// </summary>
    public JsonNode? App { get; set; }
}

