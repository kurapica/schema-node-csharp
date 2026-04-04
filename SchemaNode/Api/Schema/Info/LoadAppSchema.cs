using Microsoft.Extensions.Logging;
using SchemaNode.Components;
using SchemaNode.Enum;
using SchemaNode.Http;
using SchemaNode.Runtime;
using SchemaNode.Schema;

namespace SchemaNode.Api.Schema.Info;

/// <summary>
/// The LoadSchema api
/// </summary>
public class LoadAppSchemaApi : SchemaApi<LoadAppSchemaRequest, LoadAppSchemaResponse>
{
    /// <inheritdoc />
    protected override async Task<LoadAppSchemaResponse?> ExecuteAsync(LoadAppSchemaRequest request,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("[Api]LoadAppSchemaApi [Request]{request}", request);

        AppType? node = await SchemaContext.GetAppTypeAsync(request.Name);
        if (node == null) return new LoadAppSchemaResponse();
        
        // authorize
        await SchemaContext.AuthorizeAsync(node, PolicyScope.SchemaRead);

        // Download file for the given format
        if (!string.IsNullOrWhiteSpace(request.Format))
        {
            ISchemaFormatProvider? provider = ISchemaFormatProvider.GetSchemaFormatProvider(request.Format);
            if (provider != null)
            {
                SchemaApiFile? output = await provider.GenerateAppSchemaOutput(SchemaContext, node, request.Format, cancellationToken);
                if (output != null)
                {
                    return new LoadAppSchemaResponse
                    {
                        Output = output
                    };
                }
            }
        }

        // Generate schema
        AppSchema schema = new()
        {
            Name = node.Name,
            Display = node.Display,
            ScopePolicy = node.ScopePolicy,
            Auth = node.Auth?.Name,
            Auths = node.Auths,
            Status = node.Status,
            HasApps = node.Apps is { Length: > 0 },
            HasFields = node.Fields is { Count: > 0 },
            Workflows = node.Workflows?.Select(w => (AppWorkflowSchema)w).ToArray(),
            Extensions = node.Extensions,
            Apps = node.Apps?.Select(a => {
                AppType? childNode = node.SubAppList?.Values.FirstOrDefault(p => p.Name.Equals(a.Name, StringComparison.OrdinalIgnoreCase));
                return new AppSchema
                {
                    Name = a.Name,
                    Display = a.Display,
                    ScopePolicy = a.ScopePolicy,
                    Auth = a.Auth,
                    Auths = a.Auths,
                    Extensions = a.Extensions,
                    Status = node.Status,
                    HasApps = (a.HasApps ?? false) || a.Apps is { Length: > 0 } || childNode?.Apps is {  Length: > 0 },
                    HasFields = (a.HasFields ?? false) || a.Fields is { Length: > 0 } || childNode?.Fields is { Count: > 0},
                };
            }).ToArray(),
        };

        if (node.Fields is { Count: > 0 })
        {
            schema.Fields = node.Fields.Select(p => (AppFieldSchema)p).ToArray();
            schema.Relations = node.Relations?.Select(r => new StructRelationSchema
            {
                Field = !string.IsNullOrEmpty(r.DataField) ? $"{r.AppField}.{r.DataField}" : r.AppField,
                Prop = r.Prop,
                Func = r.Func,
                Args = r.Args.Select(a => new FuncCallArg
                {
                    Name = !string.IsNullOrEmpty(a.DataField) ? $"{a.AppField}.{a.DataField}" : a.AppField,
                    Value = a.Value,
                }).ToArray()
            }).ToArray();

            if(request.IncludeTypes)
                schema.NodeSchemas = await node.GetNodeSchemas(SchemaContext, includeUsedBy: true, cancellationToken: cancellationToken);
        }

        return new LoadAppSchemaResponse
        {
            Schema = schema,
        };
    }
}

/// <summary>
/// The LoadSchema request
/// </summary>
public class LoadAppSchemaRequest : SchemaApiRequest
{
    /// <summary>
    /// The app schema name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether include the schema types
    /// </summary>
    public bool IncludeTypes { get; set; }

    /// <summary>
    /// The app schema format for download
    /// </summary>
    public string? Format { get; set; }
}

/// <summary>
/// The LoadSchema response
/// </summary>
public class LoadAppSchemaResponse : SchemaApiResponse
{
    /// <summary>
    /// The app schema
    /// </summary>
    public AppSchema? Schema { get; set; }
}