using System.Collections;
using System.Text.Json.Nodes;
using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using static SchemaNode.Utility.Constant;
using SchemaNode.Property.Core;
using SchemaNode.Property.Function;
using StructType = SchemaNode.Runtime.StructType;
using SchemaNode.Runtime;

// ReSharper disable InconsistentNaming

namespace SchemaNode.Function;

/// <summary>
/// System.Collection Aps
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_COLLECTION)]
public static class SystemCollection
{
    /// <summary>
    /// Creates a new array
    /// </summary>
    public static T[] newarray<T>(params T[] items) => items;

    /// <summary>
    /// Gets the array length
    /// </summary>
    public static long length([Meta<SchemaType>(NS_SYSTEM_ARRAY)] object array)
    {
        return array switch
        {
            string str => str.Length,
            JsonArray jsonArray => jsonArray.Count,
            Array arr => arr.LongLength,
            ArrayNode node => node.Count,
            ICollection collection => collection.Count,
            _ => 0
        };
    }
    
    /// <summary>
    /// Whether the list contains the item
    /// </summary>
    [Meta<Logic>(LogicType.Contains)]
    public static bool contains<T>(ArrayNode array, T value) where T: IComparable
    {
        return array.Any(item => EqualityComparer<T>.Default.Equals(item.GetValue<T>(), value));
    }

    /// <summary>
    /// Whether the list not contains the item
    /// </summary>
    [Meta<Logic>(LogicType.NotContains)]
    public static bool notcontains<T>(ArrayNode array, T value) where T: IComparable
    {
        return !contains(array, value);
    }

    /// <summary>
    /// Gets the field value from the object
    /// </summary>
    public static async Task<T?> getfield<T>(SchemaContext context, DataNode obj, string field, T? @default)
    {
        DataNode? result = await GetFieldNode(context, obj, field);
        return result is { IsEmpty: false } ? result.GetValue<T>() : (@default ?? default);
    }

    /// <summary>
    /// Gets fields from the objects in the array to a new array
    /// </summary>
    public static async Task<ArrayNode> getfields(SchemaContext context, ArrayNode array, string field)
    {
        if (array.ElementType is not StructType @struct) throw new InvalidOperationException("The array type is invalid");
        
        var f = @struct.GetField(field)?? throw new InvalidOperationException($"The field {field} not found in the struct {@struct.Name}");
        if (f.Type == null) throw new InvalidOperationException($"The field {field} type is null in the struct {@struct.Name}");
        var arrayNode = await context.GetArrayNodeTypeAsync(f.Type) ?? throw new InvalidOperationException($"The field {field} type {f.Type} has no array type");

        ArrayNode resultType = new (arrayNode);
        foreach (DataNode item in array)
        {
            DataNode? fieldNode = await GetFieldNode(context, item, field);
            if (fieldNode != null && !fieldNode.IsEmpty) resultType.Add(fieldNode);
        }
        return resultType;
    }
    
    /// <summary>
    /// order by the given field
    /// </summary>
    public static ArrayNode orderby(ArrayNode obj, string field, bool descending)
    {
        var list = new List<IValueAccess>(obj);
        list.Sort((a, b) =>
        {
            if (a is not StructNode sa || b is not StructNode sb) return 0;
            var fa = sa.GetAccessValue(field);
            var fb = sb.GetAccessValue(field);
            if ((fa == null || fa.IsEmpty) && (fb == null || fb.IsEmpty)) return 0;
            if (fa == null || fa.IsEmpty) return descending ? 1 : -1;
            if (fb == null || fb.IsEmpty) return descending ? -1 : 1;
            if (fa.GetValue<IComparable>() is {} ca && fb.GetValue<IComparable>() is {} cb)
                return descending ? cb.CompareTo(ca) : ca.CompareTo(cb);
            return 0;
        });
        var node = new ArrayNode(obj.Type);
        node.AddRange(list);
        return node;
    }
    
    /// <summary>
    /// Skip the given count items
    /// </summary>
    public static ArrayNode skip(ArrayNode obj, int count)
    {
        if (count <= 0) return obj;
        var node = new ArrayNode(obj.Type);
        node.AddRange(obj.Skip(count));
        return node;
    }

    /// <summary>
    /// Take the given count items
    /// </summary>
    public static ArrayNode take(ArrayNode obj, int count)
    {
        if (count >= obj.Count) return obj;
        var node = new ArrayNode(obj.Type);
        node.AddRange(obj.Take(count));
        return node;
    }
    
    /// <summary>
    /// Gets the field node from object
    /// </summary>
    static async Task<DataNode?> GetFieldNode(SchemaContext context, DataNode? obj, string path)
        => obj is not StructNode s ? obj?.GetAccessValue(path) as DataNode : await s.GetFieldValueAsync(context, path);
}