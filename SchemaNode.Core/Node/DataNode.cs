using SchemaNode.Utility;
using System.Text.Json.Nodes;
using ValueType = SchemaNode.Runtime.ValueType;
// ReSharper disable InconsistentNaming
// ReSharper disable VirtualMemberCallInConstructor

namespace SchemaNode.Node;

public abstract class DataNode
{
    public DataNode(ValueType type, object? value = null)
    {
        Type = type;
        Value = value;
    }

    /// <summary>
    /// The value type
    /// </summary>
    public ValueType Type { get; }

    // The internal value
    protected object? _value;

    /// <summary>
    /// The c# type representation
    /// </summary>
    public virtual Type? CsharpType => Type.ToCsharpType();

    /// <summary>
    /// Violated Constraints
    /// </summary>
    public string[]? Violated { get; internal set; }

    /// <summary>
    /// Whether the node is valid, which means no violated constraints
    /// </summary>
    public virtual bool IsValid => Violated is not { Length: > 0 };

    /// <summary>
    /// indicate whether the node is empty
    /// </summary>
    public virtual bool IsEmpty => _value == null;

    /// <summary>
    /// Convert to value
    /// </summary>
    public virtual T? ToValue<T>() => ToTypeValue(typeof(T)) is T val ? val : default;

    /// <summary>
    /// The value equals
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public virtual bool Equals(DataNode other) => ReferenceEquals(this, other) || Equals(_value, other._value);

    /// <summary>
    /// Convert to type value
    /// </summary>
    public virtual object? ToTypeValue(Type type) => type.TryConvert(_value);

    /// <summary>
    /// Convert to type value with generic type
    /// </summary>
    public T? ToTypeValue<T>() => ToTypeValue(typeof(T)) is T val ? val : default;

    /// <summary>
    /// The value of the node
    /// </summary>
    public virtual object? Value
    {
        get => _value;
        set
        {
            if (value is DataNode node)
            {
                if (node == this) return;
                value = node.Value;
            }
            if (value == null)
                _value = null;
            else if (CsharpType is {} type)
            {
                _value = type.TryConvert(value);
            }
            else
            {
                _value = value;
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

    /// <summary>
    /// Gets the value by source
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    public virtual DataNode? GetSourceValue(ReadOnlySpan<char> source) => source.IsEmpty ? this : null;

    /// <summary>
    /// Refresh violated constraints based on data node structure
    /// </summary>
    public virtual void RefreshViolatedConstraints() { }

    /// <summary>
    /// Gets the source value by path
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public DataNode? GetSourceValue(string path)
    {
        SpanReader reader = path;
        DataNode? curr = this;
        while (curr != null && reader.NextPath())
            curr = curr.GetSourceValue(reader.Current);
        return curr;
    }
}
