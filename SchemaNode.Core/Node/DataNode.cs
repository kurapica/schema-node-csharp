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
    private List<IConstraintProperty>? _violated;
    
    #endregion
    
    #region Implementation
    
    /// <summary>
    /// Gets the access value by path
    /// </summary>
    public virtual IValueAccess? GetAccessValue(string path, IValueAccess? node = null)
        => (string.IsNullOrWhiteSpace(path) || path.Equals(NODE_SELF, StringComparison.OrdinalIgnoreCase))  ? this : null;

    /// <inheritdoc/>
    public void RecordConstraint(IConstraintProperty constraint, bool result)
    {
        if (result)
        {
            if (_violated == null) return;
            for (int i = _violated.Count - 1; i >= 0; i--)
            {
                if (_violated[i].Equals(constraint) || !constraint.Stackable && constraint.GetType() == _violated[i].GetType())
                    _violated.RemoveAt(i);
            }
        }
        else
        {
            _violated ??= [];
            _violated.Add(constraint);
        }
    }

    /// <inheritdoc/>
    public IEnumerable<IConstraintProperty> GetViolatedConstraints()
    {
        if (_violated == null) yield break;
        foreach (var constraint in _violated) 
            yield return constraint;
    }

    /// <summary>
    /// Whether the node is valid, which means no violated constraints
    /// </summary>
    public virtual bool IsValid => _violated is not { Count: > 0 };

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