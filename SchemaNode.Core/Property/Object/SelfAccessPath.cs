using SchemaNode.Property.Common;
using SchemaNode.Runtime;
using SchemaNode.Struct;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Object;

/// <summary>
/// The self access path property, which indicates that the node can access itself
/// </summary>
public class SelfAccessPath: Property<bool>, IAccessPathProperty
{
    /// <inheritdoc/>
    public IEnumerable<Entry<string>> GetAccessEntries(IValueTypeAccess owner)
    {
        var entry = new Entry<string> { Value = NODE_SELF };
        entry.SetProperty<Display, LocaleString>(NODE_SELF);
        yield return entry;
    }

    /// <inheritdoc/>
    public IValueTypeAccess? GetAccessValueType(IValueTypeAccess owner, string path)
    {
        return owner;
    }

    /// <inheritdoc/>
    public bool IsMatch(IValueTypeAccess owner, string path)
    {
        return NODE_SELF.Equals(path, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public IValueAccess? GetAccessValue(IValueAccess owner, string path, IValueAccess? node = null)
    {
        return IsMatch(owner.Type, path) ? (node ?? owner) : null;
    }
}