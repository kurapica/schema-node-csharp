using SchemaNode.Runtime;

namespace SchemaNode.Node;

public abstract class ScalarNode : DataNode
{
    public ScalarNode(ScalarType type, object? value = null) : base(type, value)
    {
    }
}

/// <summary>
///  For bool node
/// </summary>
public class BoolNode : DataNode
{
    public override bool IsEmpty => _value == null;

    public BoolNode(BoolType type, object? value = null) : base(type, value)
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
