using System.Collections.Immutable;
using SchemaNode.Utility;
using ValueType = SchemaNode.Runtime.ValueType;
// ReSharper disable InconsistentNaming
// ReSharper disable VirtualMemberCallInConstructor

namespace SchemaNode.Node;

/// <summary>
/// The data node interface, which represents a node in the data structure. It can be a value node, an array node, or a struct node.
/// </summary>
public abstract class DataNode
{
    #region Properties

    /// <summary>
    /// The value type
    /// </summary>
    public ValueType Type { get; init; } = null!;

    /// <summary>
    /// Violated Constraints
    /// </summary>
    public ImmutableArray<string>? Violated { get; private set; }
    
    #endregion
    
    #region Methods
    
    /// <summary>
    /// Gets the access value by path
    /// </summary>
    public DataNode? GetAccessValue(string path)
    {
        SpanReader reader = path;
        DataNode? curr = this;
        while (curr != null && reader.NextPath())
            curr = curr.GetAccessValue(reader.Current);
        return curr;
    }
    
    /// <summary>
    /// Sets violated constraints, which will be used to determine whether the node is valid
    /// </summary>
    public void SetViolated(IEnumerable<string>? violated = null, IEnumerable<string>? passed = null, bool? reset = null)
    {
        IEnumerable<string>? v = reset == true || Violated == null ? violated : violated != null ? Violated.Concat(violated) : Violated;
        if (passed is not null) v = v?.Except(passed, StringComparer.OrdinalIgnoreCase);
        Violated = v?.Distinct(StringComparer.OrdinalIgnoreCase)?.ToImmutableArray();
    }

    /// <summary>
    /// Sets violated constraints, which will be used to determine whether the node is valid
    /// </summary>
    public void SetViolated(params string[] violated) => SetViolated(violated, null, false);
    
    /// <summary>
    /// Clear violated constraints
    /// </summary>
    public void ClearViolated(params string[] passed) => SetViolated(null, passed, false);

    #endregion
    
    #region Abstract
    
    /// <summary>
    /// indicate whether the node has value
    /// </summary>
    public abstract bool IsEmpty { get; }

    /// <summary>
    /// Try set value to the data node
    /// </summary>
    public abstract bool TrySetValue<T>(T? value);

    /// <summary>
    /// Try gets the value as the given type
    /// </summary>
    public abstract bool TryGetValue<T>(out T? value);
    
    #endregion

    #region Virtual

    /// <summary>
    /// The c# type representation
    /// </summary>
    public virtual Type? CsharpType => Type.ToCsharpType();

    /// <summary>
    /// Gets the access value by part path
    /// </summary>
    public virtual DataNode? GetAccessValue(ReadOnlySpan<char> source) => source.IsEmpty ? this : null;

    /// <summary>
    /// Refresh violated constraints based on data node structure
    /// </summary>
    public virtual void RefreshViolated() { }

    /// <summary>
    /// Whether the node is valid, which means no violated constraints
    /// </summary>
    public virtual bool IsValid => Violated is not { Length: > 0 };
    
    /// <summary>
    /// Equals check
    /// </summary>
    public virtual bool Equals(DataNode? other) => other != null && (ReferenceEquals(this, other) || IsEmpty ? other.IsEmpty : TryGetValue(out object? thisValue) && other.TryGetValue(out object? otherValue) && Equals(thisValue, otherValue));

    #endregion
}