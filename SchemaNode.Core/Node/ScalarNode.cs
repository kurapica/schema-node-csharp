using SchemaNode.Runtime;

namespace SchemaNode.Node;

/// <summary>
///  For bool node
/// </summary>
public class BoolNode : DataNode
{
    public override bool IsEmpty => _value == null;
    
    internal BoolNode(BoolType type, object? value = null) : base(type, value)
    {
    }
}

/// <summary>
///  For string node
/// </summary>
public class StringNode : DataNode
{
    public override bool IsEmpty => _value == null || string.IsNullOrWhiteSpace(_value.ToString());
    
    internal StringNode(StringType type, object? value = null) : base(type, value)
    {
    }
}

/// <summary>
///  For numeric node
/// </summary>
public class NumericNode : DataNode
{
    public override bool IsEmpty => _value == null || string.IsNullOrWhiteSpace(_value.ToString());
    
    internal NumericNode(DecimalType type, object? value = null) : base(type, value)
    {
    }
}

/// <summary>
///  For int node
/// </summary>
public class IntNode : DataNode
{
    public override bool IsEmpty => _value == null || string.IsNullOrWhiteSpace(_value.ToString());
    
    internal IntNode(IntType type, object? value = null) : base(type, value)
    {
    }
}

/// <summary>
///  For date node
/// </summary>
public class DateNode : DataNode
{
    public override bool IsEmpty => _value == null || string.IsNullOrWhiteSpace(_value.ToString());
    
    internal DateNode(DateType type, object? value = null) : base(type, value)
    {
    }
}
