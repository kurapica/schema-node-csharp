using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Schema;
using System.Text.Json.Nodes;

namespace SchemaNode.Runtime;

public sealed class JsonType: AnySchemaType
{
    #region State
    
    /// <inheritdoc />
    public override SchemaType Type => SchemaType.Json;

    /// <summary>
    /// Is value type
    /// </summary>
    public override bool IsValueType => true;

    #endregion

    #region Methods

    public override Task<(AnySchemaNode? value, JsonNode? error)> ValidateValueAsync(SchemaContext context, JsonNode value, IReadOnlyList<IConstraintProperty>? constraints = null)
    {
        return Task.FromResult<(AnySchemaNode? value, JsonNode? error)>((new JsonTypeNode(this, value), null));
    }

    public override ArrayType? GetArrayType(bool exactly = false)
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
        return schema?.ToSchema();
    }

    #endregion
}