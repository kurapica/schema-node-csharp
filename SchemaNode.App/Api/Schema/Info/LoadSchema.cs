using System.ComponentModel.DataAnnotations;
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
public class LoadSchemaApi : SchemaApi<LoadSchemaRequest, LoadSchemaResponse>
{
    /// <inheritdoc />
    protected override async Task<LoadSchemaResponse?> ExecuteAsync(LoadSchemaRequest request,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("[Api]LoadSchema [Request]{request}", request);

        NodeSchema root = new NodeSchema
        {
            Name = "",
            Type = SchemaType.Namespace,
            Schemas = []
        };
        HashSet<string> types = new();

        foreach (string t in request.Names)
        {
            cancellationToken.ThrowIfCancellationRequested();
            NodeType? node = await SchemaContext.GetNodeTypeAsync(t);
            await GetNodeSchemas(node, true);
        }

        return new LoadSchemaResponse
        {
            Schemas = root.Schemas
        };

        async Task GetNodeSchemas(NodeType? node, bool first = false)
        {
            if (node == null) return;
            if (await SchemaContext.AuthorizeAsync(node, PolicyScope.SchemaRead, true) == false) return;

            await node.GetNodeSchemas(SchemaContext, root, types, true, cancellationToken);

            if (node is TypeNamespace ns && (first || request.Full == true))
                foreach (KeyValuePair<string, NodeType> pair in ns.SchemaNodes)
                    await GetNodeSchemas(pair.Value);
        }
    }
}

/// <summary>
/// The LoadSchema request
/// </summary>
public class LoadSchemaRequest : SchemaApiRequest
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
public class LoadSchemaResponse : SchemaApiResponse
{
    /// <summary>
    /// The node schemas
    /// </summary>
    public NodeSchema[]? Schemas { get; set; }
}