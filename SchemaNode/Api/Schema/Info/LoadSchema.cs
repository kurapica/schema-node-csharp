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
            AnySchemeType? node = await SchemaContext.GetSchemaTypeAsync(t);
            if (node == null) continue;
            
            // authorize
            await SchemaContext.AuthorizeAsync(node, PolicyScope.SchemaRead);
            
            // Generate schema
            await node.GetNodeSchemas(SchemaContext, root, types, true, cancellationToken);

            if (node is TypeNamespace ns)
            {
                foreach (KeyValuePair<string, AnySchemeType> pair in ns.SchemaNodes)
                {
                    await pair.Value.GetNodeSchemas(SchemaContext, root, types, true, cancellationToken);
                }   
            }
        }

        return new LoadSchemaResponse
        {
            Schemas = root.Schemas
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