using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using SchemaNode.Context;
using SchemaNode.Http;
using SchemaNode.Property.App;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;
using NamespaceType = SchemaNode.Runtime.NamespaceType;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Api.Schema.Info;

/// <summary>
/// The LoadSchema api
/// </summary>
public class GetSchemaApi : SchemaApi<GetSchemaRequest, GetSchemaResponse>
{
    /// <inheritdoc />
    protected override async Task<GetSchemaResponse?> ExecuteAsync(GetSchemaRequest request,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("[Api]LoadSchema [Request]{request}", request);

        NodeSchema root = new NodeSchema
        {
            Name = "",
            Kind = SCHEMA_KIND_NAMESPACE,
            Schemas = []
        };
        HashSet<string> types = [];

        foreach (string t in request.Names)
        {
            cancellationToken.ThrowIfCancellationRequested();
            NodeType? node = await SchemaContext.GetNodeTypeAsync(t);
            await GetNodeSchemas(node, true);
        }

        return new GetSchemaResponse
        {
            Schemas = root.Schemas
        };

        async Task GetNodeSchemas(NodeType? node, bool first = false)
        {
            if (node == null) return;
            if (await SchemaContext.AuthorizeAsync(node, PolicyScope.SchemaRead, true) == false) return;

            await SchemaContext.GetNodeSchemasAsync(node, root, types, request.Full ?? false, true, cancellationToken);

            if (node is NamespaceType ns && first && request.Full != true)
                foreach (var pair in ns.GetNodeSchemas())
                {
                    var nodeType = await SchemaContext.GetNodeTypeAsync(pair.FullName);
                    if (nodeType != null)
                        await GetNodeSchemas(nodeType);
                }
        }
    }
}

/// <summary>
/// The LoadSchema request
/// </summary>
public class GetSchemaRequest : SchemaApiRequest
{
    /// <summary>
    /// The schema names
    /// </summary>
    [Required]
    [MinLength(1)]
    public string[] Names { get; set; } = [];

    /// <summary>
    /// Full namespace
    /// </summary>
    public bool? Full { get; set; }
}

/// <summary>
/// The LoadSchema response
/// </summary>
public class GetSchemaResponse : SchemaApiResponse
{
    /// <summary>
    /// The node schemas
    /// </summary>
    public NodeSchema[]? Schemas { get; set; }
}