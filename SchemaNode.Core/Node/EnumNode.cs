using SchemaNode.Runtime;
using SchemaNode.Utility;
using System.Text.Json.Nodes;

namespace SchemaNode.Node;

public class EnumNode : DataNode
{
    internal EnumNode(EnumType type, object? value = null) : base(type, value)
    {
    }
    
    /// <summary>
    /// Convert to json node
    /// </summary>
    public override System.Text.Json.Nodes.JsonNode? ToJson() => _value == null ? null : (NodeType as EnumType)!.ValueType == Enum.EnumValueType.String ? _value.ToJsonNode() : JsonValue.Create(_value is long ? _value : (int)_value);

    /// <summary>
    /// Convert to literal value
    /// </summary>
    public override object? LiteralValue => _value != null ? ((NodeType as EnumType)!.ValueType == Enum.EnumValueType.String ? _value.ToString() : (_value is long ? _value : (int)_value)) : null;
    
    /// <summary>
    /// To string
    /// </summary>
    public override string ToString() => _value != null ? ((NodeType as EnumType)!.ValueType == Enum.EnumValueType.String ? _value.ToString() : (_value is long ? _value : (int)_value).ToString())! : string.Empty;
}
