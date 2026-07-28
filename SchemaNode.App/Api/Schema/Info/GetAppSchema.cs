using Microsoft.Extensions.Logging;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Http;
using SchemaNode.Property.App;
using SchemaNode.Schema;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Api.Schema.Info;

/// <summary>
/// The LoadSchema api
/// </summary>
public class GetAppSchemaApi : SchemaApi<LoadAppSchemaRequest, LoadAppSchemaResponse>
{
    /// <inheritdoc />
    protected override async Task<LoadAppSchemaResponse?> ExecuteAsync(LoadAppSchemaRequest request,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("[Api]LoadAppSchemaApi [Request]{request}", request);

        Runtime.AppType? node = await SchemaContext.GetAppTypeAsync(request.Name);
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
        AppSchema schema = await node.GetSchemaAsync(SchemaContext);
        
        if(request.IncludeTypes)
            schema.NodeSchemas = await SchemaContext.GetNodeSchemasAsync(node, includeUsedBy: true, cancellationToken: cancellationToken);

        if (schema.Fields is not { Length: > 0 })
        {
            List<AppSchema> apps = [];
            foreach (AppSchema s in node.GetSubAppSchemas())
            {
                Runtime.AppType? subApp = await SchemaContext.GetAppTypeAsync(s.FullName);
                if (subApp != null)
                    apps.Add(await subApp.GetSchemaAsync(SchemaContext));
            }
            schema.Apps = apps.ToArray();
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