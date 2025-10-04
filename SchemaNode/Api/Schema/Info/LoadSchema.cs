using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using SchemaNode.Enum;
using SchemaNode.Http;
using SchemaNode.Node;
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

        List<NodeSchema> schemas = [];
        foreach (string t in request.Names)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AnySchemaNode? node = await SchemaContext.GetSchemaNodeAsync(t);
            if (node == null) continue;
            NodeSchema schema = node!;
            if (node is NamespaceNode @ns)
            {
                // add one level sub nodes
                schema.Schemas = ns.Schemas.Select(s => {
                    AnySchemaNode? subNode = ns.SchemaNodes.GetValueOrDefault(s.Name);
                    return new NodeSchema
                    {
                        Name = s.Name,
                        Type = s.Type,
                        Display = s.Display,
                        LoadState = s.LoadState,
                        HasSchemas = subNode is NamespaceNode n && n.Schemas.Length > 0
                    };
                }).ToArray();
            }
            schemas.Add(schema);
        }

        return new LoadSchemaResponse
        {
            Schemas = schemas.ToArray()
        };
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