using System.ComponentModel;
using ModelContextProtocol.Server;
using SchemaNode.Context;
using SchemaNode.Runtime;
using SchemaNode.Schema;

namespace SchemaNode.McpHost.Tools;

[McpServerToolType]
public class SchemaTools
{
    [McpServerTool, Description("Get the node schema for a given schema type name")]
    public static async Task<NodeSchema> GetSchemaNodeAsync(
        SchemaContext context,
        [Description("The namespace of the schema node to retrieve")] string name)
    {
        AnySchemaType schemaType = await context.GetSchemaTypeAsync(name)
            ?? throw new InvalidOperationException($"Schema type '{name}' not found.");

        return await schemaType.GetNodeSchemas(context);
    }
}