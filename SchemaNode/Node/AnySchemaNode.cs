using SchemaNode.Enum;
using SchemaNode.Runtime;
using SchemaNode.Utility;
using System.Text.Json.Nodes;

namespace SchemaNode.Node;

public abstract class AnySchemaNode
{
    internal AnySchemaNode(AnySchemeType type, object? value = null) {
        Type = type;
        CsharpType = type.ToCSharpType();

        if (value != null) Value = value;
    }

    /// <summary>
    /// The schema type representation
    /// </summary>
    public virtual AnySchemeType Type { get; set; }

    /// <summary>
    /// The c# type representation
    /// </summary>
    public Type CsharpType { get; set; }

    /// <summary>
    /// The schema type
    /// </summary>
    public SchemaType SchemaType => Type.Type;

    public virtual bool IsEmpty => _value == null;

    public virtual T? ToValue<T>() => ToTypeValue(typeof(T)) is T val ? val : default;

    public virtual object? ToTypeValue(Type type) => type.TryConvert(_value);

    /// <summary>
    /// The value of the node
    /// </summary>
    public virtual object? Value
    {
        get => _value;
        set
        {
            if (value is AnySchemaNode node)
            {
                _value = node.Type.CanBeUseAs(Type) ? CsharpType.TryConvert(node.Value) : throw new InvalidCastException();
            }
            else
            {
                _value = CsharpType.TryConvert(value);
            }
        }
    }

    /// <summary>
    /// Convert to json node
    /// </summary>
    public virtual JsonNode? ToJson() => _value?.ToJsonNode();

    /// <summary>
    /// To string
    /// </summary>
    public override string ToString() => _value?.ToJson() ?? string.Empty;

    internal object? _value;
}
