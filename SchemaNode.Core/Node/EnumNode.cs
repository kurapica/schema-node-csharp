﻿using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Runtime;
using SchemaNode.Utility;
using System.Collections;
using System.Text.Json.Nodes;

namespace SchemaNode.Node;

/// <summary>
/// The enum data node
/// </summary>
public class EnumNode : ScalarNode
{
    private string? _strValue;
    private long? _longValue;
    private string? _originalStr;
    private long? _originalLong;
    private readonly bool _isString;

    #region Constructors

    public EnumNode(ISchemaContext context, EnumType type, object? value = null, IValueAccess? parent = null, params IPropertyProvider[] propertyProviders)
        : base(context, type, null, parent, propertyProviders)
    {
        _isString = type.Type == EnumValueType.String;
        if (value is not null)
        {
            if (!TrySetValue(value))
                throw new InvalidCastException($"Failed to set value to schema type {type.Name}.");
            if (FullMode)
                Confirm();
        }
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
            if (string.Equals(_strValue, str)) return true;
            _strValue = str;
            _longValue = null;
        }
        else
        {
            if (!value.TryConvertTo(out long longValue)) return false;
            if (_longValue == longValue) return true;
            _longValue = longValue;
            _strValue = null;
        }
        OnNext(GetRawValue());
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
    public override bool TryGetOriginal(Type type, out object? value)
    {
        if (_isString)
        {
            if (type.TryConvert(_originalStr, out var val))
            {
                value = val;
                return true;
            }
        }
        else if (type.TryConvert(_originalLong, out var val))
        {
            value = val;
            return true;
        }
        value = null;
        return false;
    }

    /// <inheritdoc/>
    public override bool Changed => _isString
        ? !(string.IsNullOrWhiteSpace(_originalStr) && string.IsNullOrWhiteSpace(_strValue)) && !string.Equals(_originalStr, _strValue)
        : !(_originalLong is null && _longValue is null) && _originalLong != _longValue;

    /// <inheritdoc/>
    public override void Confirm()
    {
        _originalStr = _strValue;
        _originalLong = _longValue;
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        if (_isString)
            _strValue = _originalStr;
        else
            _longValue = _originalLong;
        OnNext(GetRawValue());
    }

    /// <inheritdoc/>
    public override void ClearValue()
    {
        if (IsEmpty) return;
        _strValue = null;
        _longValue = null;
        OnNext(GetRawValue());
    }

    /// <inheritdoc/>
    public override bool Equals(IValueAccess? other)
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
    public override IValueAccess Clone()
    {
        EnumNode node = new(Context, (Type as EnumType)!, null, null, PropertyProviders.ToArray())
        {
            _longValue = _longValue,
            _strValue = _strValue,
            _originalLong = _originalLong,
            _originalStr = _originalStr
        };
        return node;
    }

    private object? GetRawValue() => _isString ? _strValue : _longValue;
}

/// <summary>
/// The enum array data node, a scalar holding an array of enum values with the enum type properties applied.
/// Mirrors the TypeScript <c>EnumArrayNode</c> which extends <c>ScalarNode</c>.
/// </summary>
public class EnumArrayNode : ScalarNode
{
    private string?[]? _strValues;
    private long[]? _longValues;
    private string?[]? _originalStr;
    private long[]? _originalLong;
    private readonly bool _isString;

    /// <summary>
    /// The enum element type
    /// </summary>
    public EnumType EnumType { get; }

    public EnumArrayNode(ISchemaContext context, ArrayType type, object? value = null, IValueAccess? parent = null, params IPropertyProvider[] propertyProviders)
        : this(context, type, (type.Element as EnumType) ?? throw new Exception($"The type '{type.Name}' is not a valid enum array type."), value, parent, propertyProviders)
    {
    }

    private EnumArrayNode(ISchemaContext context, ArrayType type, EnumType enumType, object? value, IValueAccess? parent, IPropertyProvider[] propertyProviders)
        : base(context, type, value, parent, new EnumArrayNodePropertyProvider(enumType, propertyProviders))
    {
        EnumType = enumType;
        _isString = enumType.Type == EnumValueType.String;
    }

    /// <inheritdoc/>
    public override bool IsEmpty => _isString
        ? _strValues is not { Length: > 0 }
        : _longValues is not { Length: > 0 };

    /// <inheritdoc/>
    public sealed override bool TrySetValue<T>(T? value) where T : default
    {
        if (value is null)
        {
            if (IsEmpty) return true;
            _strValues = null;
            _longValues = null;
            OnNext(GetRawValue());
            return true;
        }

        if (value is EnumArrayNode other)
        {
            if (other == this) return true;
            _strValues = other._strValues?.ToArray();
            _longValues = other._longValues?.ToArray();
            OnNext(GetRawValue());
            return true;
        }

        if (_isString)
        {
            string?[]? arr = ToArray<string?>(value);
            if (arr == null) return false;
            _strValues = arr;
            _longValues = null;
        }
        else
        {
            long[]? arr = ToArray<long>(value);
            if (arr == null) return false;
            _longValues = arr;
            _strValues = null;
        }
        OnNext(GetRawValue());
        return true;
    }

    /// <inheritdoc/>
    public override bool TryGetValue(Type type, out object? value)
    {
        if (type == typeof(object) || type.IsAssignableFrom(GetType()))
        {
            value = this;
            return true;
        }

        if (type == typeof(JsonArray) || type == typeof(JsonNode))
        {
            value = GetJsonArray();
            return true;
        }

        if (type == typeof(string))
        {
            value = GetJsonArray().ToJsonString();
            return true;
        }

        if (type == typeof(IEnumerable))
        {
            value = GetJsonArray().ToList();
            return true;
        }

        if (_isString)
        {
            if (type.TryConvert(_strValues, out var val))
            {
                value = val;
                return true;
            }
        }
        else if (type.TryConvert(_longValues, out var val))
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
        if (TryGetValue(typeof(T), out object? converted))
        {
            if (converted is null)
            {
                value = default;
                return true;
            }

            if (converted is T typed)
            {
                value = typed;
                return true;
            }
        }

        value = default;
        return false;
    }

    /// <inheritdoc/>
    public override bool TryGetOriginal(Type type, out object? value)
    {
        if (_isString)
        {
            if (type.TryConvert(_originalStr, out var val))
            {
                value = val;
                return true;
            }
        }
        else if (type.TryConvert(_originalLong, out var val))
        {
            value = val;
            return true;
        }
        value = null;
        return false;
    }

    /// <inheritdoc/>
    public override bool Changed => _isString
        ? !ArrayEqual(_originalStr, _strValues)
        : !ArrayEqual(_originalLong, _longValues);

    /// <inheritdoc/>
    public override void Confirm()
    {
        _originalStr = _strValues?.ToArray();
        _originalLong = _longValues?.ToArray();
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        if (_isString)
            _strValues = _originalStr?.ToArray();
        else
            _longValues = _originalLong?.ToArray();
        OnNext(GetRawValue());
    }

    /// <inheritdoc/>
    public override void ClearValue()
    {
        if (IsEmpty) return;
        _strValues = null;
        _longValues = null;
        OnNext(GetRawValue());
    }

    /// <inheritdoc/>
    public override bool Equals(IValueAccess? other)
    {
        if (other == null) return IsEmpty;
        if (_isString)
        {
            if (other.TryGetValue(out string?[]? val))
                return ArrayEqual(_strValues, val);
        }
        else
        {
            if (other.TryGetValue(out long[]? val2))
                return ArrayEqual(_longValues, val2);
        }
        return false;
    }

    /// <inheritdoc/>
    public override IValueAccess Clone()
    {
        EnumArrayNode node = new(Context, (Type as ArrayType)!, EnumType, null, null, PropertyProviders.ToArray())
        {
            _strValues = _strValues?.ToArray(),
            _longValues = _longValues?.ToArray(),
            _originalStr = _originalStr?.ToArray(),
            _originalLong = _originalLong?.ToArray()
        };
        return node;
    }

    private object? GetRawValue() => _isString ? _strValues : _longValues;

    private JsonArray GetJsonArray()
    {
        JsonArray result = [];
        if (_isString)
        {
            foreach (string? v in _strValues ?? [])
                result.Add(JsonValue.Create(v));
        }
        else
        {
            foreach (long v in _longValues ?? [])
                result.Add(JsonValue.Create(v));
        }
        return result;
    }

    private static T[]? ToArray<T>(object? value)
    {
        if (value is null) return null;
        if (value is T[] typed) return typed;
        if (value is System.Collections.IEnumerable enumerable)
        {
            List<T> list = [];
            foreach (var item in enumerable)
            {
                if (!item.TryConvertTo(out T? v) || v is null) return null;
                list.Add(v);
            }
            return list.ToArray();
        }
        return value.TryConvertTo(out T[]? result) ? result : null;
    }

    private static bool ArrayEqual<T>(T[]? a, T[]? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null) return false;
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (!EqualityComparer<T>.Default.Equals(a[i], b[i])) return false;
        return true;
    }
}

/// <summary>
/// The property provider that merges the enum type properties with the given providers
/// </summary>
internal sealed class EnumArrayNodePropertyProvider(EnumType enumType, IPropertyProvider[] providers) : IPropertyProvider
{
    /// <inheritdoc/>
    public T? GetProperty<T>() where T : class, IProperty
    {
        foreach (var provider in providers)
            if (provider.GetProperty<T>() is { HasValue: true } prop)
                return prop;
        return enumType.GetProperty<T>();
    }

    /// <inheritdoc/>
    public IEnumerable<T> GetProperties<T>() where T : IProperty
    {
        foreach (var prop in providers.SelectMany(p => p.GetProperties<T>()))
            yield return prop;
        foreach (var prop in enumType.GetProperties<T>())
            yield return prop;
    }
}
