using SchemaNode.Runtime;
using SchemaNode.Struct;

namespace SchemaNode.Property;

/// <summary>
/// The interface for access path property, which indicates that the path of a node to access other nodes
/// </summary>
public interface IAccessPathProperty: IProperty
{
    /// <summary>
    /// Gets the access entries for the given owner
    /// </summary>
    IEnumerable<Entry<string>> GetAccessEntries(IValueTypeAccess owner);
    
    /// <summary>
    /// Gets the access value type
    /// </summary>
    IValueTypeAccess? GetAccessValueType(IValueTypeAccess owner, string path);
    
    /// <summary>
    /// Whether the given path is a valid access path for the given owner
    /// </summary>
    bool IsMatch(IValueTypeAccess owner, string path);
    
    /// <summary>
    /// Gets the access value
    /// </summary>
    IValueAccess? GetAccessValue(IValueAccess owner, string path, IValueAccess? node = null);
}