using SchemaNode.Runtime;
using SchemaNode.Utility;
using System.Text.Json.Nodes;

namespace SchemaNode.Node;

public class EnumTypeNode : AnySchemaNode
{
    internal EnumTypeNode(EnumType type, object? value = null) : base(type, value)
    {
    }
    
    /// <summary>
    /// Convert to json node
    /// </summary>
    public override JsonNode? ToJson() => _value == null ? null : (SchemaType as EnumType)!.ValueType == Enum.EnumValueType.String ? _value.ToJsonNode() : JsonValue.Create(_value is long ? _value : (int)_value);

    /// <summary>
    /// Convert to literal value
    /// </summary>
    public override object? LiteralValue => _value != null ? ((SchemaType as EnumType)!.ValueType == Enum.EnumValueType.String ? _value.ToString() : (_value is long ? _value : (int)_value)) : null;
    
    /// <summary>
    /// To string
    /// </summary>
    public override string ToString() => _value != null ? ((SchemaType as EnumType)!.ValueType == Enum.EnumValueType.String ? _value.ToString() : (_value is long ? _value : (int)_value).ToString())! : string.Empty;
}
