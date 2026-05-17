using SchemaNode.Utility;

namespace SchemaNode.Node;

public abstract class ScalarNode<T> : DataNode
{
    private T? _value;
    
    /// <inheritdoc/>
    public override bool IsEmpty => _value == null;

    /// <inheritdoc/>
    public override bool TryGetValue<T1>(out T1? value) where T1 : default
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public override bool TrySetValue<T1>(T1? value) where T1 : default
    {
        _value = value.TryConvertTo<T>();
        return true;
    }
    
    /// <inheritdoc/>
    public override bool Equals(DataNode? other) => other is ScalarNode<T> scalarNode && Equals(_value, scalarNode._value);
}

/// <summary>
///  For bool node
/// </summary>
public class BoolNode : ScalarNode<bool>
{
    
    // Parses a string to a bool (accepts "true"/"false"/0/1)
    static bool TryParseBoolValue(string? value, out bool ret)
    {
        ret = false;
        if (string.IsNullOrEmpty(value)) return false;
        value = value.ToLower();
        switch (value)
        {
            case "true":  ret = true;  return true;
            case "false": ret = false; return true;
            default:
                if (!int.TryParse(value, out int val) || val is < 0 or > 1) return false;
                ret = val == 1;
                return true;
        }
    }
}

/// <summary>
///  For string node
/// </summary>
public class StringNode : ScalarNode<string>;

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
