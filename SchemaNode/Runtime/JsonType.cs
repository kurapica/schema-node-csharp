using System.Text.Json.Nodes;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Schema;

namespace SchemaNode.Runtime;

public class JsonType: AnySchemeType
{
    #region State
    
    /// <inheritdoc />
    public override SchemaType Type => SchemaType.Json;

    #endregion
    
    #region Methods

    public override Task<(AnySchemaNode? value, JsonNode? error)> ValidateValueAsync(SchemaContext context, JsonNode value)
    {
        return Task.FromResult<(AnySchemaNode? value, JsonNode? error)>((new JsonTypeNode(this, value), null));
    }

    public override ArrayType? GetArrayNode(bool exactly = false)
    {
        return null;
    }

    #endregion
    
    #region Conversion
    
    /// <summary>
    /// Convert the node to schema
    /// </summary>
    public static implicit operator NodeSchema?(JsonType? schema)
    {
        if (schema == null) return null;
        return new NodeSchema
        {
            Name = schema.Name,
            Type = schema.Type,
            Display = schema.Display,
            LoadState = schema.LoadState,
            Auth = schema.Auth?.Name,
            Used = schema.IsUsed,
        };
    }

    #endregion
}