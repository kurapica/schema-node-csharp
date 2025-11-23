using SchemaNode.Components.Context;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using System.Collections.Concurrent;

namespace SchemaNode.Components;

public static class ContextExtension
{
    /// <summary>
    /// Gets the context item by field name, like @user.name
    /// </summary>
    public static AnySchemaNode? GetSchemaContextItem(this SchemaContext context, string field)
    {
        if (field.StartsWith("@")) field = field[1..]; // remove @ prefix
        string[] paths = field.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (paths.Length == 0) return null;

        if (!ItemProvider.TryGetValue(paths[0], out (string schemaType, Type providerType) set)) return null;

        // Gets the field type
        AnySchemeType type = context.GetSchemaTypeAsync(set.schemaType).GetAwaiter().GetResult()!;

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
    /// Gets the context item type by field name, like @user.name
    /// </summary>
    public static AnySchemeType? GetSchemaContextItemType(this SchemaContext context, string field)
    {
        if (field.StartsWith("@")) field = field[1..]; // remove @ prefix
        string[] paths = field.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (paths.Length == 0) return null;

        if (!ItemProvider.TryGetValue(paths[0], out (string schemaType, Type providerType) set)) return null;

        // Gets the field type
        AnySchemeType? type = context.GetSchemaTypeAsync(set.schemaType).GetAwaiter().GetResult()!;
        for (int i = 1; i < paths.Length; i++)
        {
            while (type is StructType @struct)
            {
                StructFieldConfig? f = @struct.Fields?.FirstOrDefault(f => f.Name.Equals(paths[i], StringComparison.OrdinalIgnoreCase));
                if (f == null) return null;
                type = f.TypeNode;
            }
        }
        return type;
    }

    /// <summary>
    /// The context item providers
    /// </summary>
    internal static readonly ConcurrentDictionary<string, (string schemaType, Type providerType)> ItemProvider = new();
}
