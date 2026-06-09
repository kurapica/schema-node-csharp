using System.Collections.Concurrent;
using SchemaNode.Property.Constraints;

namespace SchemaNode.Context;

/// <summary>
/// The schema context item provider, don't implement directly
/// </summary>
public interface ISchemaContextItemProvider
{
    /// <summary>
    /// Whether the item is available
    /// </summary>
    bool HasItem { get; }
    
    /// <summary>
    /// Gets the item
    /// </summary>
    object GetItem();
    
    /// <summary>
    /// Try to get the item
    /// </summary>
    bool TryGetItem(out object? item);
}

/// <summary>
/// The schema context item provider with given type
/// </summary>
/// <typeparam name="T"></typeparam>
public interface ISchemaContextItemProvider<T>: ISchemaContextItemProvider
{
    /// <summary>
    /// Gets the item
    /// </summary>
    new T GetItem();

    /// <summary>
    /// Try to get the item
    /// </summary>
   public bool TryGetItem(out T? item)
    {
        try
        {
            item = GetItem();
            return true;
        }
        catch
        {
            item = default(T?);
            return false;
        }
    }

    #region Implementation

    object ISchemaContextItemProvider.GetItem() => GetItem()!;
    
    bool ISchemaContextItemProvider.TryGetItem(out object? item)
    {
        bool result = TryGetItem(out T? typedItem);
        item = typedItem;
        return result;
    }

    #endregion
}

/// <summary>
/// The schema context item providers
/// </summary>
internal class SchemaContextItemProvider(Type[] providers)
{
    /// <summary>
    /// The context item providers
    /// </summary>
    readonly ConcurrentDictionary<string, (string SchemaType, Type ProviderType, Type ItemType)> _itemProvider = new(StringComparer.OrdinalIgnoreCase);
    readonly ConcurrentDictionary<Type, string> _typeFieldMap = new();

    public Type[] Providers { get; } = providers;
    
    /// <summary>
    /// Gets provide information
    /// </summary>
    internal (string SchemaType, Type ProviderType, Type ItemType)? GetProviderType(string field)
    {
        if (_itemProvider.TryGetValue(field, out var provider))
            return provider;
        return null;
    }
    
    /// <summary>
    /// Gets provide information
    /// </summary>
    internal (string SchemaType, Type ProviderType, Type ItemType)? GetProviderType(Type type)
    {
        if (!_typeFieldMap.TryGetValue(type, out string? field)) return null;
        if (_itemProvider.TryGetValue(field, out var provider)) return provider;
        return null;
    }
    
    /// <summary>
    /// Gets the provider type
    /// </summary>
    internal IEnumerable<(string SchemaType, Type ProviderType, Type ItemType)> GetProviderTypes => _itemProvider.Values;

    /// <summary>
    /// Gets provide information
    /// </summary>
    internal (string SchemaType, Type ProviderType, Type ItemType)? GetProviderType<T>() => GetProviderType(typeof(T));
    
    internal void BindSchemaContextItemProvider(string field, string schemaType, Type providerType, Type itemType)
    {
        field = field.ToLower();
        _itemProvider[field] = (SchemaType: schemaType, ProviderType: providerType,  ItemType: itemType);
        _typeFieldMap[itemType] = field;
    }
}