namespace SchemaNode.Components.Context;

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
    bool TryGetItem(out T? item)
    {
        try
        {
            item = GetItem();
            return true;
        }
        catch
        {
            item = default;
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