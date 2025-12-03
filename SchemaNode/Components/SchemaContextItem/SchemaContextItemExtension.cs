using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Runtime;
using System.Collections.Concurrent;

namespace SchemaNode.Components;

/// <summary>
/// The schema context item extension
/// Unlike context item, the schema context is registered with item providers
/// </summary>
public static class SchemaContextItemExtension
{
    /// <summary>
    /// Gets the context item by field name, like @user.name
    /// </summary>
    public static AnySchemaNode? GetSchemaContextItem(this SchemaContext context, string field)
    {
        string[] paths = field.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (paths.Length == 0) return null;

        if (!ItemProvider.TryGetValue(paths[0], out (string schemaType, Type providerType) set)) return null;

        // Gets the field type
        AnySchemeType type = context.GetSchemaTypeAsync(set.schemaType).GetAwaiter().GetResult()!;

        // Check context item first
        if (context.TryGetContextItem(set.providerType, out object? setItem))
        {
            AnySchemaNode? node = setItem is AnySchemaNode n 
                ? n
                : type.CreateNode(setItem!);
            if (paths.Length > 1)
            {
                return node is StructTypeNode @struct
                    ? @struct.GetValueByPaths(paths.Skip(1))
                    : null;
            }
            return node;
        }
        
        // Gets the item provider
        if (context.GetService(set.providerType) is ISchemaContextItemProvider { HasItem: true } providerInstance
            && providerInstance.TryGetItem(out object? item))
        {
            AnySchemaNode? node = type.CreateNode(item);
            if (paths.Length > 1)
            {
                return node is StructTypeNode @struct
                    ? @struct.GetValueByPaths(paths.Skip(1))
                    : null;
            }
            return node;
        }
        return null;
    }

    /// <summary>
    /// Gets the schema context item by type
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="context"></param>
    /// <returns></returns>
    public static T? GetSchemaContextItem<T>(this SchemaContext context)
    {
        if (!typeFieldMap.TryGetValue(typeof(T), out string? field)) return null;
        if (!ItemProvider.TryGetValue(field, out (string schemaType, Type providerType) set)) return null;

        // Check context item first
        if (context.TryGetContextItem(set.providerType, out object? setItem))
        {
            return setItem is AnySchemaNode n
                ? n.ToValue<T>()
                : setItem as T;
        }

        // Gets the item provider
        if (context.GetService(set.providerType) is ISchemaContextItemProvider { HasItem: true } provider
            && provider.TryGetItem(out object? item))
            return item as T;
        return default;
    }

    /// <summary>
    /// Copys the schema context item from source to target
    /// </summary>
    public static void CopySchemaContextItem(this SchemaContext context, SchemaContext source)
    {
        foreach (var (key, (_, providerType)) in ItemProvider)
        {
            var node = source.GetSchemaContextItem(key);
            if (node == null) continue;
            context.SetContextItem(providerType, node);
        }
    }

    internal static void BindSchemaContextItemProvider(string field, string schemaType, Type providerType, Type itemType)
    {
        ItemProvider[field] = (schemaType, providerType);
        typeFieldMap[itemType] = field;
    }

    /// <summary>
    /// The context item providers
    /// </summary>
    static readonly ConcurrentDictionary<string, (string schemaType, Type providerType)> ItemProvider = new();
    static readonly ConcurrentDictionary<Type, string> typeFieldMap = new();
}
