using System.Collections.Immutable;
using SchemaNode.Context;
using SchemaNode.Utility;
using ValueType = SchemaNode.Runtime.ValueType;
// ReSharper disable InconsistentNaming
// ReSharper disable VirtualMemberCallInConstructor

namespace SchemaNode.Node;

/// <summary>
/// The data node interface, which represents a node in the data structure. It can be a value node, an array node, or a struct node.
/// </summary>
public interface IDataNode: IEquatable<IDataNode>
{
    #region Abstract
    
    /// <summary>
    /// The value type
    /// </summary>
    ValueType Type { get; init; }

    /// <summary>
    /// Violated Constraints
    /// </summary>
    ImmutableArray<string>? Violated { get; protected set; }
    
    /// <summary>
    /// indicate whether the node is empty
    /// </summary>
    bool IsEmpty { get; }

    /// <summary>
    /// Set value to the data node without validation
    /// </summary>
    /// <param name="value"></param>
    /// <typeparam name="T"></typeparam>
    void SetValue<T>(T? value);
    
    /// <summary>
    /// Gets the value as the given type
    /// </summary>
    T? GetValue<T>();
    
    #endregion

    #region Virtual

    /// <summary>
    /// The c# type representation
    /// </summary>
    public virtual Type? CsharpType => Type.ToCsharpType();

    /// <summary>
    /// Whether the node is valid, which means no violated constraints
    /// </summary>
    public virtual bool IsValid => Violated is not { Length: > 0 };

    /// <summary>
    /// Gets the access value by path
    /// </summary>
    public IDataNode? GetAccessValue(string path)
    {
        SpanReader reader = path;
        IDataNode? curr = this;
        while (curr != null && reader.NextPath())
            curr = curr.GetAccessValue(reader.Current);
        return curr;
    }
    
    /// <summary>
    /// Gets the access value by part path
    /// </summary>
    public virtual IDataNode? GetAccessValue(ReadOnlySpan<char> source) => source.IsEmpty ? this : null;

    /// <summary>
    /// Sets violated constraints, which will be used to determine whether the node is valid
    /// </summary>
    public void SetViolated(string[] violated, string[]? passed, bool? reset = null)
    {
        IEnumerable<string> v = reset == true || Violated == null ? violated : Violated.Concat(violated);
        if (passed is not null)
            v = v.Where(x => !passed.Contains(x, StringComparer.OrdinalIgnoreCase));
        Violated = [..v.Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// Refresh violated constraints based on data node structure
    /// </summary>
    public virtual void RefreshViolated() { }

    #endregion
}