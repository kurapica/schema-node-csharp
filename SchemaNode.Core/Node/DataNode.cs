using System.Collections.Immutable;
using SchemaNode.Utility;
using ValueType = SchemaNode.Runtime.ValueType;
using static SchemaNode.Utility.Constant;
using SchemaNode.Property;
using SchemaNode.Runtime;

// ReSharper disable InconsistentNaming
// ReSharper disable VirtualMemberCallInConstructor
// ReSharper disable ConditionalAccessQualifierIsNonNullableAccordingToAPIContract

namespace SchemaNode.Node;

/// <summary>
/// The data node interface, which represents a node in the data structure. It can be a value node, an array node, or a struct node.
/// </summary>
public abstract class DataNode : IValueAccess
{
    #region Properties

    /// <summary>
    /// The value type
    /// </summary>
    public ValueType Type { get; init; } = null!;

    /// <summary>
    /// The parent
    /// </summary>
    public IValueAccess? Parent { get; init; }

    /// <summary>
    /// Violated Constraints
    /// </summary>
    public ImmutableArray<string>? Violated { get; private set; }
    
    #endregion
    
    #region Methods
    
    /// <summary>
    /// Gets the access value by path
    /// </summary>
    public IValueAccess? GetAccessValue(string path, IValueAccess? node = null)
    {
        SpanReader reader = path;
        DataNode? curr = this;
        while (curr != null && reader.NextPath())
            curr = curr.GetAccessValue(reader.Current, node);
        return curr;
    }
    
    /// <summary>
    /// Sets violated constraints, which will be used to determine whether the node is valid
    /// </summary>
    public void SetViolated(IEnumerable<IProperty>? violated = null, IEnumerable<IProperty>? passed = null, bool? reset = null)
    {
        var violatedNames = violated?.Select(v => v.Name);
        IEnumerable<string>? v = reset == true || Violated == null ? violatedNames : violatedNames != null ? Violated.Concat(violatedNames) : Violated;
        if (passed is not null) v = v?.Except(passed.Where(p => !p.Stackable).Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
        Violated = v?.Distinct(StringComparer.OrdinalIgnoreCase)?.ToImmutableArray();
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
    public void SetViolated(params IProperty[] violated) => SetViolated(violated, null, false);
    
    /// <summary>
    /// Clear violated constraints
    /// </summary>
    public void ClearViolated(params IProperty[] passed) => SetViolated(null, passed, false);

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
    public abstract bool TryGetValue(Type type, out object? value);

    /// <summary>
    /// Clear value
    /// </summary>
    public virtual void ClearValue() => TrySetValue<object>(null);

    /// <summary>
    /// Try gets the value as the given type
    /// </summary>
    public virtual bool TryGetValue<T>(out T? value)
    {
        bool isEmpty = IsEmpty;
        if (isEmpty || !TryGetValue(typeof(T), out object? obj))
        {
            value = default(T?);
            return isEmpty;
        }
        value = (T?)obj;
        return true;
    }
    
    /// <summary>
    /// Gets value
    /// </summary>
    public T? GetValue<T>() => TryGetValue(out T? value) ? value : default(T?);
    
    /// <summary>
    /// Gets value
    /// </summary>
    public object? GetValue(Type type) => TryGetValue(type, out object? value) ? value : null;

    /// <summary>
    /// Clones the data node
    /// </summary>
    public abstract DataNode Clone();
    
    #endregion

    #region Virtual

    /// <summary>
    /// The c# type representation
    /// </summary>
    public virtual Type? CsharpType => Type.GetCsharpType();
    
    /// <summary>
    /// Gets the access value by part path
    /// </summary>
    public virtual DataNode? GetAccessValue(ReadOnlySpan<char> source, IValueAccess? node = null) 
        => source.IsEmpty || source.SeqEquals(NODE_SELF, StringComparison.OrdinalIgnoreCase) ? this : null;

    /// <summary>
    /// Whether the node is valid, which means no violated constraints
    /// </summary>
    public virtual bool IsValid => Violated is not { Length: > 0 };
    
    /// <summary>
    /// Equals check
    /// </summary>
    public virtual bool Equals(DataNode? other) 
        => other != null && 
           (ReferenceEquals(this, other) || 
            IsEmpty 
               ? other.IsEmpty 
               : TryGetValue(out object? thisValue) && 
                 other.TryGetValue(out object? otherValue) && 
                 Equals(thisValue, otherValue));

    /// <inheritdoc/>
    public override string? ToString() => GetValue<string>();
    
    #endregion
}