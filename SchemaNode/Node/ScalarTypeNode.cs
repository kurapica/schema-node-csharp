using SchemaNode.Runtime;

namespace SchemaNode.Node;

/// <summary>
///  For common scalar node
/// </summary>
public class ScalarTypeNode : AnySchemaNode
{
    public override bool IsEmpty => _value == null || string.IsNullOrEmpty(_value.ToString());
    
    internal ScalarTypeNode(ScalarType type, object? value = null) : base(type, value)
    {
    }
}