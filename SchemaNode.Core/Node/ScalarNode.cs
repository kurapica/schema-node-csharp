using SchemaNode.Utility;

namespace SchemaNode.Node;

public abstract class ScalarNode : DataNode;

public abstract class ScalarNode<T> : ScalarNode
{
    protected T? Value;
    
    /// <inheritdoc/>
    public override bool IsEmpty => Value == null || Value.Equals(default(T));

    /// <inheritdoc/>
    public override bool TrySetValue<T1>(T1? value) where T1 : default
    {
        if (!value.TryConvertTo<T>(out var result)) return false;
        Value = result;
        return true;
    }

    /// <inheritdoc/>
    public override bool TryGetValue(Type type, out object? value)
    {
        if (type.TryConvert(Value, out var result))
        {
            value = result;
            return true;
        }
        value = null;
        return false;
    }

    /// <inheritdoc/>
    public override bool TryGetValue<T1>(out T1? value) where T1 : default
    {
        if (Value.TryConvertTo<T1>(out var result))
        {
            value = result;
            return true;
        }
        value = default(T1?);
        return false;
    }

    /// <inheritdoc/>
    public override void ClearValue() => Value = default(T?);

    /// <inheritdoc/>
    public override bool Equals(DataNode? other) => other is ScalarNode<T> scalarNode && Equals(Value, scalarNode.Value) ||
                                                    other is EnumNode enumNode && enumNode.TryGetValue(out T? val) && Equals(Value, val);
}

/// <summary>
/// Object data node
/// </summary>
public class AnyNode : ScalarNode<object>
{
    public override DataNode Clone()
    {
        return new AnyNode
        {
            Type = Type,
            Value = Value
        };
    }
}

/// <summary>
///  Bool data node
/// </summary>
public class BoolNode : ScalarNode<bool>
{
    public override DataNode Clone()
    {
        return new BoolNode()
        {
            Type = Type,
            Value = Value
        };
    }
}

/// <summary>
///  String data node
/// </summary>
public class StringNode : ScalarNode<string>
{
    public override DataNode Clone()
    {
        return new StringNode
        {
            Type = Type,
            Value = Value
        };
    }
}

/// <summary>
///  Numeric data node
/// </summary>
public class DecimalNode : ScalarNode<decimal>
{
    public override DataNode Clone()
    {
        return new DecimalNode
        {
            Type = Type,
            Value = Value
        };
    }
}

/// <summary>
/// Int data node
/// </summary>
public class IntNode : ScalarNode<long>
{
    public override DataNode Clone()
    {
        return new IntNode
        {
            Type = Type,
            Value = Value
        };
    }
}

/// <summary>
/// DateTime data node
/// </summary>
public class DateNode : ScalarNode<DateTimeOffset>
{
    public override DataNode Clone()
    {
        return new DateNode
        {
            Type = Type,
            Value = Value
        };
    }
}
