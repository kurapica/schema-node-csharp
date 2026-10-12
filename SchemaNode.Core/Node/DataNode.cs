using static SchemaNode.Utility.Constant;
using SchemaNode.Property;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using System.Text.Json.Nodes;
using SchemaNode.Context;
using SchemaNode.Property.Struct;
using SchemaNode.Property.Common;

// ReSharper disable InconsistentNaming
// ReSharper disable VirtualMemberCallInConstructor
// ReSharper disable ConditionalAccessQualifierIsNonNullableAccordingToAPIContract

namespace SchemaNode.Node;

/// <summary>
/// The data node interface, which represents a node in the data structure. It can be a value node, an array node, or a struct node.
/// </summary>
public abstract class DataNode : IValueAccess
{
    #region Relation

    /// <summary>
    /// Gets the relation that effect this node for the given property
    /// </summary>
    public (RelationType Relation, IValueAccess Owner)? GetRelation<T>() where T: IProperty
    {
        IValueAccess? curr = this.Parent;
        RelationType? last = null;
        IValueAccess? owner = null;
        while (curr != null)
        {
            foreach(RelationType r in curr!.Type.GetRelations())
            {
                if (r.Property?.GetCsharpType() != typeof(T)) continue;
                if (curr.GetAccessValue(r.Target, this) != this) continue;
                last = r;
                owner = curr;
                break;
            }
            curr = curr.Parent;
        }
        return last != null ? (last, owner!) : null;
    }

    /// <summary>
    /// Loading default value for node
    /// </summary>
    public async Task LoadDefaultAsync(SchemaContext context)
    {
        // For now only load default value for displayonly field
        if (!this.IsEmpty || this.PropertyProvider?.GetProperty<DisplayOnly>()?.Value != true) return;
        var r = this.GetRelation<Default>();
        if (r is null) return;
        var d = await r.Value.Relation.ProcessAsync(context, r.Value.Owner, this);
        if (d is null || !d.HasValue) return;
        this.TrySetValue(d.GetValue<object>());
    }

    /// <summary>
    /// Clear displayOnly node to avoid un-valid data
    /// </summary>
    public virtual void ClearDisplayOnlyNode() {
        if (this.PropertyProvider?.GetProperty<DisplayOnly>()?.Value == true)
            this.ClearValue();
    }

    #endregion

    #region Properties

    /// <summary>
    /// The value type
    /// </summary>
    public IValueTypeAccess Type { get; init; } = null!;

    /// <summary>
    /// The parent
    /// </summary>
    public IValueAccess? Parent { get; init; }
    
    /// <summary>
    /// The property provider
    /// </summary>
    public IPropertyProvider? PropertyProvider { get; init; }

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
    {
        if (string.IsNullOrEmpty(path)) return this;
        if (path.Equals(NODE_SELF, StringComparison.OrdinalIgnoreCase)) return node ?? this;
        if (path.Equals(TYPE_PROVIDER, StringComparison.OrdinalIgnoreCase))
        {
            var access = node ?? this;
            while (access != null && access.PropertyProvider?.GetProperty<TypeProvider>() is not
                       { HasValue: true })
                access = access.Parent;
            if (access?.PropertyProvider?.GetProperty<TypeProvider>() is { HasValue: true } type)
                return access.GetAccessValue(type.GetValue<string>()!, node);
        }
        return null;
    }

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
        else if (_violated == null || _violated.All(v => v != constraint && (v.Stackable || v.GetType() != constraint.GetType())))
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

    /// <summary>
    /// Gets the violated
    /// </summary>
    public virtual JsonNode? Violated => _violated is {  Count: > 0 } ? new JsonArray(_violated.Select(v => JsonValue.Create(v.Name)).ToArray()) : null;

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
    public abstract IValueAccess Clone();
    
    #endregion

    #region Virtual

    /// <summary>
    /// The c# type representation
    /// </summary>
    public virtual Type? CsharpType => (Type as Runtime.ValueType)?.GetCsharpType();
    
    /// <summary>
    /// Equals check
    /// </summary>
    public virtual bool Equals(IValueAccess? other) 
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