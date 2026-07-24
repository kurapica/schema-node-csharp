using SchemaNode.Enum;
using SchemaNode.Runtime;
using SchemaNode.Utility;

namespace SchemaNode.Node;

public class EnumNode : DataNode
{
    private string? _strValue;
    private long? _longValue;
    private readonly bool _isString;

    #region Constructors
    
    public EnumNode(EnumType type, IValueAccess? parent = null)
    {
        Type = type;
        Parent = parent;
        _isString = type.Type == EnumValueType.String;
    }
    
    public EnumNode(EnumType type, object value, IValueAccess? parent = null): this(type, parent)
    {
        if (!TrySetValue(value))
            throw new InvalidCastException($"Failed to set value to schema type {type.Name}.");
    }
    
    #endregion

    /// <inheritdoc/>
    public override bool IsEmpty => _isString ? string.IsNullOrWhiteSpace(_strValue) : _longValue == null;
    
    /// <inheritdoc/>
    public sealed override bool TrySetValue<T>(T? value) where T : default
    {
        if (_isString)
        {
            if (!value.TryConvertTo(out string? str)) return false;
            _strValue = str;
        }
        else
        {
            if (!value.TryConvertTo(out long longValue)) return false;
            _longValue = longValue;
        }
        return true;
    }

    /// <inheritdoc/>
    public override bool TryGetValue(Type type, out object? value)
    {
        if (_isString)
        {
            if (type.TryConvert(_strValue, out var val))
            {
                value = val;
                return true;
            }
        }
        else if (type.TryConvert(_longValue, out var val))
        {
            value = val;
            return true;
        }
        value = null;
        return false;
    }

    /// <inheritdoc/>
    public override bool TryGetValue<T>(out T? value) where T : default
    {
        if (_isString)
        {
            if (_strValue.TryConvertTo(out T? val))
            {
                value = val;
                return true;
            }
        }
        else if (_longValue.TryConvertTo(out T? val))
        {
            value = val;
            return true;
        }
        value = default(T?);
        return false;
    }

    /// <inheritdoc/>
    public override void ClearValue()
    {
        _strValue = null;
        _longValue = null;
    }

    /// <inheritdoc/>
    public override bool Equals(DataNode? other)
    {
        if (other == null) return IsEmpty;
        if (_isString)
        {
            switch (other)
            {
                case StringNode stringNode when stringNode.TryGetValue(out string? val):
                    return string.Equals(_strValue, val);
                case EnumNode enumNode when enumNode.TryGetValue(out string? val2):
                    return string.Equals(_strValue, val2);
            }
        }
        else
        {
            switch (other)
            {
                case IntNode intNode when intNode.TryGetValue(out long val):
                    return _longValue == val;
                case EnumNode enumNode when enumNode.TryGetValue(out long val2):
                    return _longValue == val2;
            }
        }
        return false;
    }

    /// <inheritdoc/>
    public override DataNode Clone()
    {
        return new EnumNode((Type as EnumType)!)
        {
            _longValue = _longValue,
            _strValue = _strValue
        };
    }
}