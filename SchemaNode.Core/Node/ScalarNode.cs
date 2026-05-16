using SchemaNode.Utility;
using ValueType = SchemaNode.Runtime.ValueType;

namespace SchemaNode.Node;

public abstract class ScalarNode<T> : IDataNode
{
    private T? _value;
    
    /// <inheritdoc/>
    public required ValueType Type { get; init; }
    
    /// <inheritdoc/>
    public string[]? Violated { get; set; }
    
    /// <inheritdoc/>
    public bool IsEmpty => _value == null;
    
    /// <inheritdoc/>
    public void SetValue<T1>(T1? value)
        => _value = value != null ? value.TryConvertTo<T>() : default(T?);
    
    /// <inheritdoc/>
    public T1? GetValue<T1>() 
        => _value != null ? _value.TryConvertTo<T1>() : default(T1?);

    /// <inheritdoc/>
    public bool Equals(IDataNode? other)
        => other is ScalarNode<T> scalarNode && GetType() == scalarNode.GetType() && Equals(_value, scalarNode._value);
}

/// <summary>
///  For bool node
/// </summary>
public class BoolNode : ScalarNode<bool>;

/// <summary>
///  For string node
/// </summary>
public class StringNode : ScalarNode<string>
{
    
}

/// <summary>
///  For numeric node
/// </summary>
public class NumericNode : ScalarNode<decimal>;

/// <summary>
///  For int node
/// </summary>
public class IntNode : ScalarNode<long>;

/// <summary>
///  For date node
/// </summary>
public class DateNode : ScalarNode<DateTimeOffset>;
