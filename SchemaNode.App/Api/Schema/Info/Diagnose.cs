using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using SchemaNode.Context;
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
            Namespace = await DiagnoseNamespaceAsync(await SchemaContext.GetNodeTypeAsync("")),
            App = DiagnoseAppAsync(await SchemaContext.GetAppTypeAsync("")),
        };
    }

    private async Task<JsonNode?> DiagnoseNamespaceAsync(NodeType? schema)
    {
        switch (schema)
        {
            case null:
                return null;
            case NamespaceType ns:
                JsonObject? res = null;
                foreach (var s in ns.GetNodeSchemas())
                {
                    var n = await SchemaContext.GetNodeTypeAsync(s.FullName);
                    if (n == null) continue;
                    var r = await DiagnoseNamespaceAsync(n);
                    if (r == null || r.IsEmpty()) continue;
                    res ??= new JsonObject();
                    res[s.Name] = r;
                }

                return res;
            default:
                return !string.IsNullOrWhiteSpace(schema.Error) ? schema.Error : null;
        }
    }

    private static JsonNode? DiagnoseAppAsync(AppType? app)
    {
        if (app == null) return null;
        if (!string.IsNullOrWhiteSpace(app.Error)) return app.Error;

        foreach (AppFieldType field in app.GetFields())   
        {
            if (!string.IsNullOrWhiteSpace(field.Error)) return field.Error;
        }

        foreach (AppWorkflowType workflow in app.GetWorkflows())
        {
            if (!string.IsNullOrWhiteSpace(workflow.Error)) return workflow.Error;
        }
        
        JsonObject? res = null;
        foreach (AppType a in app.GetSubApps())
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

