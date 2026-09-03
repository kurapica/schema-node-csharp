using SchemaNode.Context;
using SchemaNode.Property;
using SchemaNode.Struct;

namespace SchemaNode.Runtime;

/// <summary>
/// The value type access interface, which indicates that the node has access to other value types
/// </summary>
public interface IValueTypeAccess
{
    /// <summary>
    /// The type name
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// The type kind
    /// </summary>
    string Kind { get; }
    
    /// <summary>
    /// Gets the access value type
    /// </summary>
    IValueTypeAccess? GetAccessValueType(string path);

    /// <summary>
    /// Gets the access entries
    /// </summary>
    IEnumerable<Entry<string>> GetAccessEntries();

    /// <summary>
    /// Is assignable to the other value type access
    /// </summary>
    bool IsAssignableTo(IValueTypeAccess other);

    /// <summary>
    /// Whether has access entries
    /// </summary>
    /// <returns></returns>
    bool HasAccessEntries { get; }

    /// <summary>
    /// Create the value access for the given parent and property provider
    /// </summary>
    /// <param name="parent"></param>
    /// <param name="propertyProvider"></param>
    /// <returns></returns>
    IValueAccess Create(IValueAccess? parent = null, IPropertyProvider? propertyProvider = null);

    /// <summary>
    /// Generate the data node with given value
    /// </summary>
    IValueAccess From(object? value, IValueAccess? parent = null, IPropertyProvider? propertyProvider = null);

    /// <summary>
    /// Gets the match C# type
    /// </summary>
    Type? GetCsharpType(bool nullable = false);

    /// <summary>
    /// Validate value and return the access wrap
    /// </summary>
    Task<IValueAccess?> ValidateValueAsync(ISchemaContext context, object? value);
}

/// <summary>
/// The value access interface, which indicates that the node has access to other values
/// </summary>
public interface IValueAccess
{
    /// <summary>
    /// The value access type
    /// </summary>
    IValueTypeAccess Type { get;  }
    
    /// <summary>
    /// Gets the access value
    /// </summary>
    /// <param name="path">The access path from current</param>
    /// <param name="node">The access branch where the node should be in</param>
    /// <returns></returns>
    IValueAccess? GetAccessValue(string path, IValueAccess? node = null);

    /// <summary>
    /// Try set the value
    /// </summary>
    bool TrySetValue<T>(T? value);

    /// <summary>
    /// Try get the value as the given type
    /// </summary>
    bool TryGetValue<T>(out T? value);

    /// <summary>
    /// Try get the value as the given type
    /// </summary>
    bool TryGetValue(Type type, out object? value); 
    
    /// <summary>
    /// Gets the value
    /// </summary>
    public T? GetValue<T>() => TryGetValue<T>(out T? value) ? value : default(T?);

    /// <summary>
    /// Gets the value
    /// </summary>
    public object? GetValue(Type type) => TryGetValue(type, out object? value) ? value : null;
    
    /// <summary>
    /// Clear the value
    /// </summary>
    void ClearValue();
    
    /// <summary>
    /// Whether the value is empty
    /// </summary>
    bool IsEmpty { get; }

    /// <summary>
    /// The value parent
    /// </summary>
    IValueAccess? Parent { get; }
    
    /// <summary>
    /// The property provider
    /// </summary>
    IPropertyProvider? PropertyProvider { get; }

    /// <summary>
    /// Record the constraint result
    /// </summary>
    void RecordConstraint(IConstraintProperty constraint, bool result);

    /// <summary>
    /// Gets the violated constraints
    /// </summary>
    IEnumerable<IConstraintProperty> GetViolatedConstraints();

    /// <summary>
    /// Clone the value access
    /// </summary>
    IValueAccess Clone();

    /// <summary>
    /// The value is valid
    /// </summary>
    bool IsValid { get; } 
}