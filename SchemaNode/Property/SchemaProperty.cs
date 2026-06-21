using SchemaNode.Context;
using SchemaNode.Function;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Utility;
using System.Text.Json;

namespace SchemaNode.Property;

public interface IProperty
{
    /// <summary>
    /// The property name
    /// </summary>
    string Name { get; internal set; }

    /// <summary>
    /// Whether the property is for array only
    /// </summary>
    bool ForArrayOnly { get; internal set; }

    /// <summary>
    /// The property has value
    /// </summary>
    bool HasValue { get; }

    /// <summary>
    /// Sets the property value
    /// </summary>
    void SetValue(SchemaContext context, JsonElement value, AnySchemaType? type = null);

    /// <summary>
    /// Gets the adjust value
    /// </summary>
    /// <returns></returns>
    JsonElement GetValue();

    /// <summary>
    /// Gets the value as given type
    /// </summary>
    T? GetValue<T>();
}

/// <summary>
/// The base interface for all property components that can be attached to schemas, such like presentation, constraint, etc.
/// </summary>
public abstract class SchemaProperty<T> : IProperty
{
    /// <summary>
    /// The property name
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Whether the property is for array only
    /// </summary>
    public bool ForArrayOnly { get; set; }

    /// <summary>
    /// The property value
    /// </summary>
    public T? Value { get; private set; }

    /// <summary>
    /// Sets the property value
    /// </summary>
    public void SetValue(SchemaContext context, JsonElement value, AnySchemaType? valueType = null)
    {
        Type type = typeof(T);

        // for target node type
        if (type.IsAssignableTo(typeof(AnySchemaNode)))
        {
            var vtype = (valueType as ArrayType)?.ElementSchemaType ?? valueType;
            if (vtype == null) return;

            if (type == typeof(ArrayTypeNode) || type == typeof(AnySchemaNode) && valueType is ArrayType)
            {
                if (value.ValueKind != JsonValueKind.Array) return;
                var array = new ArrayTypeNode(vtype);
                foreach(var item in value.EnumerateArray())
                {
                    AnySchemaNode? node = vtype.CreateNode(item);
                    if (node != null) array.Add(node);
                }
                Value = (T)(object)array;
            }
            else
            {
                AnySchemaNode? node = valueType?.CreateNode(value);
                if (node == null || !node.GetType().IsAssignableTo(typeof(T))) return;
                Value = (T)(object)node;
            }
        }
        else
        {
            Value = value.Deserialize<T>(Extension.GetJsonOptions(false));
        }

        Init(context);
    }

    /// <summary>
    /// Gets the value
    /// </summary>
    public JsonElement GetValue()
    {
        if (!HasValue)return default;
        return JsonSerializer.SerializeToElement(Value, Extension.GetJsonOptions(false));
    }

    /// <summary>
    /// Check the value is not empty
    /// </summary>
    public virtual bool HasValue => !SystemLogic.isempty(Value);

    /// <summary>
    /// Do some init work after the value set
    /// </summary>
    public virtual void Init(SchemaContext context) { }

    /// <summary>
    /// Gets the value as given type
    /// </summary>
    T1? IProperty.GetValue<T1>() where T1 : default
    {
        if (!HasValue) return default;
        if (Value is T1 t1) return t1;
        return default;
    }
}