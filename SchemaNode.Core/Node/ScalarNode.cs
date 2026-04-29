using SchemaNode.Runtime;

namespace SchemaNode.Node;

/// <summary>
///  For common scalar node
/// </summary>
public class ScalarNode : DataNode
{
    public override bool IsEmpty => _value == null || string.IsNullOrWhiteSpace(_value.ToString());
    
    internal ScalarNode(ScalarType type, object? value = null) : base(type, value)
    {
    }
}