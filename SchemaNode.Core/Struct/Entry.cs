using System.Collections.Concurrent;
using SchemaNode.Attribute;
using SchemaNode.Property;
using SchemaNode.Property.Core;
using SchemaNode.Property.Record;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Struct;

/// <summary>
/// The dict entry
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_ENTRY, SCHEMA_KIND_ORDER_ENTRY)]
[Meta<SchemaType>(NS_SYSTEM_ENTRY)]
[Meta<Attach>(SCHEMA_KIND_ENTRY)]
public class Entry<T>: PropertyOwner where T: notnull
{
    /// <summary>
    /// The entry value
    /// </summary>
    [Meta<PrimaryIndex>]
    public T Value { get; set; } = default!;

    /// <summary>
    /// Has children
    /// </summary>
    public bool? HasChildren { get; set; }
    
    #region Runtime info

    /// <summary>
    /// The sub enum values
    /// </summary>
    private Entry<T>[]? _children;

    /// <summary>
    /// The parent of the enum value
    /// </summary>
    private Entry<T>? _parent;
    
    /// <summary>
    /// The value map, only used in the root
    /// </summary>
    private ConcurrentDictionary<T, Entry<T>>? _valueMaps;

    #endregion
    
    #region Methods
    
    /// <summary>
    /// Gets the entry access list if fully loaded
    /// </summary>
    public EntryAccess<T>[]? GetAccessList(T? value)
    {
        Entry<T>? entry = null;
        if (value is null)
        {
            if (_children is not { Length: > 0 }) return null;
            entry = this;
        }
        else
        {
            if (_valueMaps == null || !_valueMaps.TryGetValue(value, out entry)) return null;
            if (entry is { HasChildren: true, _children: not { Length: > 0 } }) return null; // not fully loaded
        }
        
        // build entry access list
        List<EntryAccess<T>> accesses = [];
        bool inBranch = false;
        while (entry != null)
        {
            accesses.Add(new EntryAccess<T>
            {
                Entry = entry.Value is not null ? entry.Clone() : null,
                Children = entry._children?.Select(c => c.Clone()).ToArray()
            });
            if (entry == this)
            {
                inBranch = true;
                break;
            }
            entry = entry._parent;
        }
        if (!inBranch) return null;

        accesses.Reverse();
        return accesses.ToArray();
    }
    
    /// <summary>
    /// Save access list
    /// </summary>
    public void SaveAccessList(EntryAccess<T>[] accesses)
    {
        _valueMaps ??= []; // only root entry will create it
        
        if (accesses.Length == 0) return;
        EntryAccess<T> current = accesses[0];

        // replace with new
        if (_children is { Length: > 0}) Array.ForEach(_children, c => c.UnRegister());
        if (current.Children is { Length: > 0 }) {
            foreach (Entry<T> v in current.Children)
            {
                v._parent = this;
                v._valueMaps = _valueMaps;
                v._children = _children?.FirstOrDefault(x => x.Value.Equals(v.Value)) is {} match
                    ? match._children
                    : null;
                v.Register();
            }
        }
        _children = current.Children;

        // for next part
        if (_children is { Length: >  0} && accesses.Length > 1 && accesses[1].Entry is {} next)
        {
            _children!.FirstOrDefault(x => x.Value.Equals(next.Value))?
                .SaveAccessList(accesses.Skip(1).ToArray());
        }
    }
    
    #endregion

    #region Utility

    private Entry<T> Clone()
    {
        Entry<T> clone = new()
        {
            Value = Value,
            HasChildren = HasChildren,
        };
        clone.CombineProperties(this);
        return clone;
    }
    
    // remove this from the value map
    private void UnRegister()
    {
        _valueMaps?.Remove(Value, out _);
        if (_children is { Length: > 0}) Array.ForEach(_children, c => c.UnRegister());
        _valueMaps = null;
        _parent = null;
    }

    // register this to the value map
    private void Register()
    {
        _valueMaps?.AddOrUpdate(Value, _ => this, (_, _) => this);
        if (_children is { Length: > 0 }) 
            Array.ForEach(_children, c =>
            {
                c._parent = this;
                c._valueMaps = _valueMaps;
                c.Register();
            });
    }

    #endregion
}

/// <summary>
/// The entry access
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_ENTRY_ACCESS)]
public class EntryAccess<T> where T: notnull
{
    /// <summary>
    /// The entry in the choose path
    /// </summary>
    public Entry<T>? Entry { get; set; }

    /// <summary>
    /// The children entries of the <see cref="Entry"/>
    /// </summary>
    public Entry<T>[]? Children { get; set; } = [];
}