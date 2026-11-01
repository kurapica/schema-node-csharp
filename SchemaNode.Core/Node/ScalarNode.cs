using SchemaNode.Context;
using SchemaNode.Runtime;
using SchemaNode.Utility;

namespace SchemaNode.Node;

public abstract class ScalarNode(ISchemaContext context, params IPropertyProvider[] propertyProviders) : DataNode(context, propertyProviders);

public abstract class ScalarNode<T>(ISchemaContext context, params IPropertyProvider[] propertyProviders) : ScalarNode(context, propertyProviders) where T: struct
{
    protected T? Value;
    
    /// <inheritdoc/>
    public override bool IsEmpty => Value is null;

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
    public override bool Equals(IValueAccess? other) => other is ScalarNode<T> scalarNode && Equals(Value, scalarNode.Value) ||
                                                        other is EnumNode enumNode && enumNode.TryGetValue(out T? val) && Equals(Value, val);
}

public abstract class RefScalarNode<T>(ISchemaContext context, params IPropertyProvider[] propertyProviders) : ScalarNode(context, propertyProviders) where T : class
{
    protected T? Value;

    /// <inheritdoc/>
    public override bool IsEmpty => Value is null;

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
    public override bool Equals(IValueAccess? other) => other is ScalarNode scalarNode && Equals(Value, scalarNode.GetValue<T>()) ||
                                                        other is EnumNode enumNode && enumNode.TryGetValue(out T? val) && Equals(Value, val);
}

/// <summary>
/// Object data node
/// </summary>
public class AnyNode(ISchemaContext context, params IPropertyProvider[] propertyProviders) : RefScalarNode<object>(context, propertyProviders)
{
    public override IValueAccess Clone()
    {
        return new AnyNode(Context, _propertyProviders.ToArray())
        {
            Type = Type,
            Value = Value
        };
    }
}

/// <summary>
///  Bool data node
/// </summary>
public class BoolNode(ISchemaContext context, params IPropertyProvider[] propertyProviders) : ScalarNode<bool>(context, propertyProviders)
{
    public override bool IsEmpty => Value is null;

    public override IValueAccess Clone()
    {
        return new BoolNode(Context, _propertyProviders.ToArray())
        {
            Type = Type,
            Value = Value
        };
    }
}

/// <summary>
///  String data node
/// </summary>
public class StringNode(ISchemaContext context, params IPropertyProvider[] propertyProviders) : RefScalarNode<string>(context, propertyProviders)
{
    public override IValueAccess Clone()
    {
        return new StringNode(Context, _propertyProviders.ToArray())
        {
            Type = Type,
            Value = Value
        };
    }
}

/// <summary>
///  Numeric data node
/// </summary>
public class DecimalNode(ISchemaContext context, params IPropertyProvider[] propertyProviders) : ScalarNode<decimal>(context, propertyProviders)
{
    public override IValueAccess Clone()
    {
        return new DecimalNode(Context, _propertyProviders.ToArray())
        {
            Type = Type,
            Value = Value
        };
    }
}

/// <summary>
/// Int data node
/// </summary>
public class IntNode(ISchemaContext context, params IPropertyProvider[] propertyProviders) : ScalarNode<long>(context, propertyProviders)
{
    public override IValueAccess Clone()
    {
        return new IntNode(Context, _propertyProviders.ToArray())
        {
            Type = Type,
            Value = Value
        };
    }
}

/// <summary>
/// DateTime data node
/// </summary>
public class DateNode(ISchemaContext context, params IPropertyProvider[] propertyProviders) : ScalarNode<DateTimeOffset>(context, propertyProviders)
{
    public override IValueAccess Clone()
    {
        return new DateNode(Context, _propertyProviders.ToArray())
        {
            Type = Type,
            Value = Value
        };
    }
}
