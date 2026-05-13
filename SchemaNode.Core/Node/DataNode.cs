using SchemaNode.Runtime;
using SchemaNode.Utility;
// ReSharper disable InconsistentNaming
// ReSharper disable VirtualMemberCallInConstructor

namespace SchemaNode.Node;

public abstract class DataNode
{
    internal DataNode(NodeType type, object? value = null) {
        NodeType = type;
        CsharpType = type.ToCSharpType();

        if (value != null) Value = value;
    }

    /// <summary>
    /// The schema type representation
    /// </summary>
    public NodeType NodeType { get; internal set; }

    /// <summary>
    /// The c# type representation
    /// </summary>
    public Type CsharpType { get; internal set; }

    /// <summary>
    /// The origin value to track the changes, also simple the event payload
    /// </summary>
    public DataNode? Origin { get; internal set; }

    /// <summary>
    /// Violated Constraints
    /// </summary>
    public string[]? ViolatedConstraints { get; internal set; }

    /// <summary>
    /// Whether the node is valid, which means no violated constraints
    /// </summary>
    public virtual bool IsValid => ViolatedConstraints == null || ViolatedConstraints.Length == 0;

    /// <summary>
    /// Gets the node error
    /// </summary>
    public virtual System.Text.Json.Nodes.JsonNode? ToError => IsValid ? null : ViolatedConstraints.ToJsonNode();

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
    public virtual bool Equals(DataNode other)
    {
        return ReferenceEquals(this, other) || object.Equals(_value, other._value);
    }

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
            if (value is DataNode node)
            {
                _value = node.NodeType.CanBeUseAs(NodeType) ? CsharpType.TryConvert(node.Value) : throw new InvalidCastException();
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
    public virtual System.Text.Json.Nodes.JsonNode? ToJson() => _value?.ToJsonNode();
    
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
    
    /// <summary>
    /// Gets the value by source
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    public abstract DataNode? GetSourceValue(ReadOnlySpan<char> source);
    
    /// <summary>
    /// Refresh violated constraints based on data node structure
    /// </summary>
    public abstract void RefreshViolatedConstraints();

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

    /// <summary>
    /// Sets the target's property value
    /// </summary>
    /// <param name="target"></param>
    /// <param name="prop"></param>
    /// <param name="value"></param>
    public abstract void SetPropertyValue(string target, string prop, DataNode? value);
}
