using SchemaNode.Enum;
using SchemaNode.Runtime;
using SchemaNode.Utility;
using System.Text.Json.Nodes;
// ReSharper disable InconsistentNaming
// ReSharper disable VirtualMemberCallInConstructor

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
    public AnySchemeType Type { get; internal set; }

    /// <summary>
    /// The c# type representation
    /// </summary>
    public Type CsharpType { get; internal set; }

    /// <summary>
    /// The origin value to track the changes, also simple the event payload
    /// </summary>
    public AnySchemaNode? Origin { get; internal set; }

    /// <summary>
    /// The schema type
    /// </summary>
    public SchemaType SchemaType => Type.Type;

    /// <summary>
    /// indicate whether the node is empty
    /// </summary>
    public virtual bool IsEmpty => _value == null;

    /// <summary>
    /// Convert to value
    /// </summary>
    public virtual T? ToValue<T>() => ToTypeValue(typeof(T)) is T val ? val : default;

    /// <summary>
    /// Convert to type value
    /// </summary>
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
    /// Convert to literal value
    /// </summary>
    public virtual object? LiteralValue => Value;

    /// <summary>
    /// To string
    /// </summary>
    public override string ToString() => _value?.ToLiteral() ?? string.Empty;

    // The internal value
    protected object? _value;
}
