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
public class GetAppSchemaApi : SchemaApi<GetAppSchemaRequest, GetAppSchemaResponse>
{
    /// <inheritdoc />
    protected override async Task<GetAppSchemaResponse?> ExecuteAsync(GetAppSchemaRequest request,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("[Api]LoadAppSchemaApi [Request]{request}", request);

        Runtime.AppType? node = await SchemaContext.GetAppTypeAsync(request.Name, token: cancellationToken).ConfigureAwait(false);
        if (node == null) return new GetAppSchemaResponse();
        
        // authorize
        await SchemaContext.AuthorizeAsync(node, PolicyScope.SchemaRead, token: cancellationToken).ConfigureAwait(false);

        // Download file for the given format
        if (!string.IsNullOrWhiteSpace(request.Format))
        {
            ISchemaFormatProvider? provider = ISchemaFormatProvider.GetSchemaFormatProvider(request.Format);
            if (provider != null)
            {
                SchemaApiFile? output = await provider.GenerateAppSchemaOutput(SchemaContext, node, request.Format, cancellationToken).ConfigureAwait(false);
                if (output != null)
                {
                    return new GetAppSchemaResponse
                    {
                        Output = output
                    };
                }
            }
        }

        // Generate schema
        AppSchema schema = await node.GetSchemaAsync(SchemaContext, token: cancellationToken).ConfigureAwait(false);
        
        if(request.IncludeTypes)
            schema.NodeSchemas = await SchemaContext.GetNodeSchemasAsync(node, includeUsedBy: true, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (schema.Fields is not { Length: > 0 })
        {
            List<AppSchema> apps = [];
            foreach (AppSchema s in node.GetSubAppSchemas())
            {
                Runtime.AppType? subApp = await SchemaContext.GetAppTypeAsync(s.FullName, token: cancellationToken).ConfigureAwait(false);
                if (subApp != null)
                    apps.Add(await subApp.GetSchemaAsync(SchemaContext, token: cancellationToken).ConfigureAwait(false));
            }
            schema.Apps = apps.ToArray();
        }

        return new GetAppSchemaResponse
        {
            Schema = schema,
        };
    }
}

/// <summary>
/// The LoadSchema request
/// </summary>
public class GetAppSchemaRequest : SchemaApiRequest
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
public class GetAppSchemaResponse : SchemaApiResponse
{
    /// <summary>
    /// The app schema
    /// </summary>
    public AppSchema? Schema { get; set; }
}