using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Property;
using SchemaNode.Property.Common;
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
[Meta<Append>(typeof(Disable), typeof(Display))]
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
    
    /// <summary>
    /// The children entries of the entry
    /// </summary>
    [SchemaIgnore]
    public Entry<T>[]? Children;

    #region Runtime info

    /// <summary>
    /// The parent of the enum value
    /// </summary>
    private Entry<T>? _parent;
    
    /// <summary>
    /// The value map, only used in the root
    /// </summary>
    private ConcurrentDictionary<T, Entry<T>>? _valueMaps;
    
    /// <summary>
    /// The entry is root
    /// </summary>
    [JsonIgnore]
    [SchemaIgnore]
    public bool IsRoot => _parent == null;

    /// <summary>
    /// The entry is fully loaded
    /// </summary>
    [JsonIgnore]
    [SchemaIgnore]
    public bool? IsFullyLoaded { get; set; }

    #endregion

    #region Methods

    /// <summary>
    /// Gets the entry by value
    /// </summary>
    public Entry<T>? GetEntry(T? value)
    {
        Entry<T>? entry = value is null ? (IsRoot ? this : null) : _valueMaps?.GetValueOrDefault(value);
        return entry != null && IsDescendant(entry) ? entry : null;
    }
    
    /// <summary>
    /// Gets the entry access list if fully loaded
    /// </summary>
    public EntryAccess<T>[]? GetAccessList(T? value)
    {
        Entry<T>? entry = value is null 
            ? this // means only get its children
            : GetEntry(value);
        if (entry is null or { HasChildren: true, Children: not { Length: > 0 } }) return null; // require load
        
        // build entry access list
        List<EntryAccess<T>> accesses = [];
        bool inBranch = false;
        while (entry != null)
        {
            accesses.Add(new EntryAccess<T>
            {
                Entry = entry._parent != null ? entry.Clone() : null,
                Children = entry.Children?.Select(c => c.Clone()).ToArray()
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
        _valueMaps ??= typeof(T) == typeof(string) 
            ? new ConcurrentDictionary<string, Entry<string>>(StringComparer.OrdinalIgnoreCase) as ConcurrentDictionary<T, Entry<T>>
            : new ConcurrentDictionary<T, Entry<T>>(); // only root entry will create it
        Entry<T>? root = this;
        
        foreach (var current in accesses)
        {
            root = root.GetEntry(current.Entry != null ? current.Entry.Value : default(T?));
            if (root is null) return; // can't save the access list

            // replace with new
            if (root.Children is { Length: > 0}) Array.ForEach(root.Children, c => c.UnRegister());
            if (current.Children is { Length: > 0 }) {
                foreach (Entry<T> v in current.Children)
                {
                    v._parent = root;
                    v._valueMaps = _valueMaps;
                    v.Children = root.Children?.FirstOrDefault(x => x.Value.Equals(v.Value)) is {} match
                        ? match.Children
                        : null;
                    v.Register();
                }
            }
            root.Children = current.Children;
            if (root.Children is {  Length: > 0 }) root.HasChildren = true; // update
        }

        // update load state
        while (root._parent is not null) root = root._parent;
        root.UpdateLoadState();
    }
    
    // Clone the entry
    public Entry<T> Clone()
    {
        Entry<T> clone = new()
        {
            Value = Value,
            HasChildren = HasChildren,
        };
        clone.CombineProperties(this);
        return clone;
    }

    #endregion

    #region Utility

    /// <summary>
    /// Refresh the load state
    /// </summary>
    private void UpdateLoadState()
    {
        if (Children is { Length: > 0 })
        {
            foreach (Entry<T> child in Children)
                child.UpdateLoadState();
            IsFullyLoaded = Children.All(c => c.IsFullyLoaded == true);
        }
        else
        {
            IsFullyLoaded = HasChildren != true;
        }
    }

    // remove this from the value map
    private void UnRegister()
    {
        _valueMaps?.Remove(Value, out _);
        if (Children is { Length: > 0}) Array.ForEach(Children, c => c.UnRegister());
        _valueMaps = null;
        _parent = null;
    }

    // register this to the value map
    private void Register()
    {
        _valueMaps?.AddOrUpdate(Value, _ => this, (_, _) => this);
        if (Children is { Length: > 0 }) 
            Array.ForEach(Children, c =>
            {
                c._parent = this;
                c._valueMaps = _valueMaps;
                c.Register();
            });
    }

    // Whether the entry is a descendant
    private bool IsDescendant(Entry<T> entry)
    {
        while (entry != this && entry._parent != null)
            entry = entry._parent;
        return entry == this;
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