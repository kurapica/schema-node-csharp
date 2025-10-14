using Microsoft.Extensions.Logging;
using SchemaNode.Http;
using SchemaNode.Node;
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

        AppNode? node = await SchemaContext.GetAppNodeAsync(request.Name);
        if (node == null) return new LoadAppSchemaResponse();

        // Generate schema
        AppSchema schema = new()
        {
            Name = node.Name,
            Display = node.Display,
            Desc = node.Desc,
            HasApps = node.Apps is { Length: > 0 },
            HasFields = node.Fields is { Count: > 0 },
            Apps = node.Apps?.Select(a => {
                AppNode? childNode = node.SubAppList?.Values.FirstOrDefault(p => p.Name.Equals(p.Name, StringComparison.OrdinalIgnoreCase));
                return new AppSchema
                {
                    Name = a.Name,
                    Display = a.Display,
                    Desc = a.Desc,
                    HasApps = (a.HasApps ?? false) || a.Apps is { Length: > 0 } || childNode?.Apps is {  Length: > 0 },
                    HasFields = (a.HasFields ?? false) || a.Fields is { Length: > 0 } || childNode?.Fields is { Count: > 0},
                };
            }).ToArray(),
        };

        if (node.Fields is { Count: > 0 })
        {
            schema.Fields = node.Fields.Select(p => (AppFieldSchema)p).ToArray();
            schema.Relations = node.Relations?.Select(r => new StructFieldRelation
            {
                Field = !string.IsNullOrEmpty(r.DataField) ? $"{r.AppField}.{r.DataField}" : r.AppField,
                Type = r.Type,
                Func = r.Func,
                Args = r.Args.Select(a => new FunctionCallArgument
                {
                    Name = !string.IsNullOrEmpty(a.DataField) ? $"{a.AppField}.{a.DataField}" : a.AppField,
                    Value = a.Value,
                }).ToArray()
            }).ToArray();

            if(request.IncludeTypes)
                schema.NodeSchemas = node.GetNodeSchemas();
        }

        return new LoadAppSchemaResponse
        {
            Schema = schema
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