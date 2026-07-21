using SchemaNode.Property;

namespace SchemaNode.Runtime;

/// <summary>
/// The node reference interface, which indicates that the node has reference to other node types
/// </summary>
public interface INodeReferences
{
    /// <summary>
    /// Gets the references
    /// </summary>
    IEnumerable<NodeType> GetReferenceTypes();
}

/// <summary>
/// The node error interface
/// </summary>
public interface IErrorProvider
{
    /// <summary>
    /// Gets the runtime node error
    /// </summary>
    string? Error { get; }
}

/// <summary>
/// The property provider for property sequence access
/// </summary>
public interface IPropertyProvider
{
    /// <summary>
    /// Gets the property
    /// </summary>
    T? GetProperty<T>() where T : class, IProperty;

    /// <summary>
    /// Gets the properties
    /// </summary>
    IEnumerable<T> GetProperties<T>() where T : IProperty;
}

/// <summary>
/// The value type access interface, which indicates that the node has access to other value types
/// </summary>
public interface IValueTypeAccess
{
    /// <summary>
    /// Gets the access value type
    /// </summary>
    ValueType? GetAccessValueType(string path);
}

/// <summary>
/// The value access interface, which indicates that the node has access to other values
/// </summary>
public interface IValueAccess
{
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
    /// Gets the value
    /// </summary>
    public T? GetValue<T>() => TryGetValue<T>(out T? value) ? value : default(T?);
    
    /// <summary>
    /// Whether the value is empty
    /// </summary>
    bool IsEmpty { get; }
    
    /// <summary>
    /// The value parent
    /// </summary>
    IValueAccess? Parent { get; }
    
    /// <summary>
    /// Record the constraint result
    /// </summary>
    void RecordConstraint(IConstraintProperty constraint, bool result);

    /// <summary>
    /// Gets the violated constraints
    /// </summary>
    IEnumerable<IConstraintProperty> GetViolatedConstraints();
    
    /// <summary>
    /// The value is valid
    /// </summary>
    public virtual bool IsValid => !GetViolatedConstraints().Any();
}