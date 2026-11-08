using SchemaNode.Context;
using SchemaNode.Property;
using SchemaNode.Struct;
using System.Reactive;
using System.Text.Json.Nodes;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SchemaNode.Runtime;


/// <summary>
/// The relation interface
/// </summary>
public interface IRelation
{
    /// <summary>
    /// The relation owner
    /// </summary>
    IValueTypeAccess? Owner { get; }

    /// <summary>
    /// The target property name
    /// </summary>
    string Target { get; }

    /// <summary>
    /// The property constructor
    /// </summary>
    Type? PropertyCtor { get; }

    /// <summary>
    /// Whether the relation depends on other value access
    /// </summary>
    bool HasDepends { get; }

    /// <summary>
    /// Attach the relation to target with the owner
    /// </summary>
    void Attach(IValueAccess owner, IValueAccess target);

    /// <summary>
    /// Detach the relation from the target with the owner
    /// </summary>
    void Detach(IValueAccess owner, IValueAccess target);

    /// <summary>
    /// Execute the relation and set new property to the target
    /// </summary>
    Task ProcessAsync(IValueAccess owner, IValueAccess target);
}

/// <summary>
/// The relation information
/// </summary>
public interface IRelationInfo
{
    /// <summary>
    /// The relation owner
    /// </summary>
    IValueAccess Owner { get; }

    /// <summary>
    /// The relations
    /// </summary>
    List<IRelation> Relations { get; }
}

/// <summary>
/// The default relation information implementation
/// </summary>
public sealed class RelationInfo : IRelationInfo
{
    /// <inheritdoc/>
    public required IValueAccess Owner { get; init; }

    /// <summary>
    /// The relations
    /// </summary>
    public List<IRelation> Relations { get; } = [];
}

/// <summary>
/// The value type access interface, which indicates that the node has access to other value types
/// </summary>
public interface IValueTypeAccess : IPropertyProvider
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
    IValueAccess Create(ISchemaContext context, object? value = null, IValueAccess? parent = null, params IPropertyProvider[] propertyProviders);

    /// <summary>
    /// Gets the match C# type
    /// </summary>
    Type? GetCsharpType(bool nullable = false);

    /// <summary>
    /// Validate value and return the access wrap
    /// </summary>
    Task<IValueAccess?> ValidateValueAsync(ISchemaContext context, object? value, CancellationToken token = default);

    /// <summary>
    /// Gets the relations types
    /// </summary>
    IEnumerable<RelationType> GetRelations();
}

/// <summary>
/// The value access interface, which indicates that the node has access to other values
/// </summary>
public interface IValueAccess
{
    /// <summary>
    /// The unqiue id(random)
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// The value access type
    /// </summary>
    IValueTypeAccess Type { get; }

    #region Value Access

    /// <summary>
    /// Whether the value is empty
    /// </summary>
    bool IsEmpty { get; }

    /// <summary>
    /// Try gets the original value
    /// </summary>
    bool TryGetOriginal<T>(out T? value);

    /// <summary>
    /// Try get the original value as the given type
    /// </summary>
    bool TryGetOriginal(Type type, out object? value);

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
    /// Gets the original value
    /// </summary>
    public T? GetOriginal<T>() => TryGetOriginal<T>(out T? value) ? value : default(T?);

    /// <summary>
    /// Gets the original value as the given type
    /// </summary>
    public object? GetOriginal(Type type) => TryGetOriginal(type, out object? value) ? value : null;

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
    /// Clone the value access
    /// </summary>
    IValueAccess Clone();

    #endregion

    #region Property Access

    /// <summary>
    /// Gets the property by type
    /// </summary>
    T? GetProperty<T>() where T : class, IProperty;

    /// <summary>
    /// Gets all the properties by type
    /// </summary>
    IEnumerable<T> GetProperties<T>() where T : IProperty;

    /// <summary>
    /// Sets the property value to the value access
    /// </summary>
    void SetPropertyValue<P, T>(T? value, IValueAccess? source = null, IRelation? relation = null) where P : Property<T>;

    /// <summary>
    /// Sets the property value to the value access by the runtime property constructor
    /// </summary>
    void SetPropertyValue(Type propertyCtor, object? value, IValueAccess? source = null, IRelation? relation = null);

    #endregion

    #region Subscription

    /// <summary>
    /// Subscribe the data change
    /// </summary>
    IDisposable Subscribe(IObserver<(IValueAccess, object?)> observer, bool immediate = false);

    /// <summary>
    /// Subscribe the data change with handler
    /// </summary>
    public IDisposable Subscribe(Action<(IValueAccess, object?)> handler, bool immediate = false)
        => Subscribe(Observer.Create(handler), immediate);

    /// <summary>
    /// Subscribe the property change
    /// </summary>
    IDisposable SubscribeProperty<P, T>(IObserver<(IValueAccess, Type, T?, T?)> observer, bool immediate = false) where P : Property<T>;

    /// <summary>
    /// Subscribe the property change with handler
    /// </summary>
    public IDisposable SubscribeProperty<P, T>(Action<(IValueAccess, Type, T?, T?)> handler, bool immediate = false) where P : Property<T> 
        => SubscribeProperty<P, T>(Observer.Create(handler), immediate);

    /// <summary>
    /// Subscribe the data violated status
    /// </summary>
    IDisposable SubscribeViolated(IObserver<(IValueAccess, bool)> observer, bool immediate = false);

    /// <summary>
    /// Subscribe the data violated status with handler
    /// </summary>
    public IDisposable SubscribeViolated(Action<(IValueAccess, bool)> handler, bool immediate = false)
        => SubscribeViolated(Observer.Create(handler), immediate);

    /// <summary>
    /// Record the disposable with source, so they can be released together
    /// </summary>
    void RecordSubscription(IDisposable disposable, object? source);

    /// <summary>
    /// Clear all the subscription with source
    /// </summary>
    void ClearSubscription(object? source);

    #endregion

    #region Path Navigation

    /// <summary>
    /// The node name
    /// </summary>
    string? Name { get; }

    /// <summary>
    /// The access path
    /// </summary>
    string Access { get; }

    /// <summary>
    /// The value parent
    /// </summary>
    IValueAccess? Parent { get; }

    /// <summary>
    /// Gets the access value
    /// </summary>
    /// <param name="path">The access path from current</param>
    /// <param name="node">The access branch where the node should be in</param>
    /// <returns></returns>
    IValueAccess? GetAccessValue(string path, IValueAccess? node = null);

    #endregion

    #region Relation

    /// <summary>
    /// Attach the relations
    /// </summary>
    void AttachRelations(IRelationInfo[] realtions);

    #endregion

    #region Validation

    /// <summary>
    /// Record the constraint result
    /// </summary>
    void RecordConstraint(IConstraintProperty constraint, bool? result);

    /// <summary>
    /// Gets the violated constraints
    /// </summary>
    IEnumerable<IConstraintProperty> GetViolatedConstraints();

    /// <summary>
    /// The value is valid
    /// </summary>
    bool IsValid { get; } 

    /// <summary>
    /// The violated data
    /// </summary>
    JsonNode? Violated { get; }

    #endregion
}

/// <summary>
/// The global value access path handler interface
/// </summary>
public interface IValueAccessPathHandler
{
    /// <summary>
    /// Load the access path handler with the given schema context
    /// </summary>
    Task LoadAsync(ISchemaContext context, CancellationToken token = default);
    
    /// <summary>
    /// The value access path
    /// </summary>
    public string Path { get; }
    
    /// <summary>
    /// The value access path display
    /// </summary>
    public LocaleString Display { get; }
    
    /// <summary>
    /// Gets the access value type
    /// </summary>
    public abstract IValueTypeAccess? GetAccessValueType(IValueTypeAccess owner);
    
    /// <summary>
    /// Gets the access value
    /// </summary>
    public abstract IValueAccess? GetAccessValue(IValueAccess owner, IValueAccess? node = null);

}