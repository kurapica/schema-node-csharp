using SchemaNode.Runtime;

namespace SchemaNode.Node;

public abstract class ScalarNode : IDataNode
{
    public ScalarNode(ScalarType type, object? value = null) : base(type, value)
    {
    }
}

/// <summary>
///  For bool node
/// </summary>
public class BoolNode : IDataNode
{
    public override bool IsEmpty => _value == null;

    public BoolNode(BoolType type, object? value = null) : base(type, value)
    {
    }
}

/// <summary>
///  For string node
/// </summary>
public class StringNode : IDataNode
{
    public override bool IsEmpty => _value == null || string.IsNullOrWhiteSpace(_value.ToString());
    
    internal StringNode(StringType type, object? value = null) : base(type, value)
    {
    }
}

/// <summary>
///  For numeric node
/// </summary>
public class NumericNode : IDataNode
{
    public override bool IsEmpty => _value == null || string.IsNullOrWhiteSpace(_value.ToString());
    
    internal NumericNode(DecimalType type, object? value = null) : base(type, value)
    {
    }
}

/// <summary>
///  For int node
/// </summary>
public class IntNode : IDataNode
{
    public override bool IsEmpty => _value == null || string.IsNullOrWhiteSpace(_value.ToString());
    
    internal IntNode(IntType type, object? value = null) : base(type, value)
    {
    }
}

/// <summary>
///  For date node
/// </summary>
public class DateNode : IDataNode
{
    public override bool IsEmpty => _value == null || string.IsNullOrWhiteSpace(_value.ToString());
    
    internal DateNode(DateType type, object? value = null) : base(type, value)
    {
    }
}
