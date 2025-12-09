using System.Collections;
using System.Numerics;
using System.Text.Json.Nodes;
using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;
// ReSharper disable InconsistentNaming

namespace SchemaNode.Function;

/// <summary>
/// System.Collection Aps
/// </summary>
[Schema(NS_SYSTEM_COLLECTION)]
public static class SystemCollection
{
    /// <summary>
    /// Gets the array length
    /// </summary>
    [Schema]
    public static long arrlen([Schema(NS_SYSTEM_ARRAY)] object array)
    {
        if (array is JsonArray jsonArray)
        {
            return jsonArray.Count;
        }
        if (array is Array arr)
        {
            return arr.LongLength;
        }
        if (array is ArrayTypeNode node)
        {
            return node.Count;
        }
        if (array is ICollection collection)
        {
            return collection.Count;
        }
        return 0;
    }

    /// <summary>
    /// Create a new array
    /// </summary>
    [Schema]
    public static List<T> newarray<T>()
    {
        return new List<T>();
    }

    /// <summary>
    /// Push to the list
    /// </summary>
    [Schema]
    public static List<T> push<T>(IEnumerable<T> arr, T value)
    {
        List<T> res = new (arr);
        res.Add(value);
        return res;
    }

    /// <summary>
    /// Whether the list contains the item
    /// </summary>
    [Schema]
    public static bool contains<T>(IEnumerable<T> array, T value) where T: IComparable
    {
        foreach (var item in array)
        {
            if (EqualityComparer<T>.Default.Equals(item, value)) return true;
        }
        return false;
    }

    /// <summary>
    /// Whether the list not contains the item
    /// </summary>
    [Schema]
    public static bool notcontains<T>(IEnumerable<T> array, T value) where T: IComparable
    {
        return !contains(array, value);
    }

    /// <summary>
    /// Calc the average
    /// </summary>
    [Schema]
    public static T average<T>(IEnumerable<T> array) where T : INumber<T>
    {
        T sum = T.Zero;
        int count = 0;
        foreach (T item in array)
        {
            count++;
            sum += item;
        }
        return count == 0 ? T.Zero : sum / T.CreateChecked(count);
    }

    /// <summary>
    /// Calc the sum
    /// </summary>
    [Schema]
    public static T sum<T>(IEnumerable<T> array) where T : INumber<T>
    {
        T sum = T.Zero;
        foreach (T item in array)
        {
            sum += item;
        }
        return sum;
    }

    /// <summary>
    /// Delete a field from the json object
    /// </summary>
    [Schema]
    public static StructTypeNode delfield(StructTypeNode obj, string field)
    {
        obj[field] = null;
        return obj;
    }

    /// <summary>
    /// Whether the object has the field
    /// </summary>
    [Schema]
    public static bool containskey(StructTypeNode obj, string field)
    {
        return obj[field] != null;
    }

    /// <summary>
    /// Whether the object not has the field
    /// </summary>
    [Schema]
    public static bool notcontainskey(StructTypeNode obj, string field)
    {
        return obj[field] == null;
    }

    /// <summary>
    /// Gets the field value from the object
    /// </summary>
    [Schema]
    public static T? getfield<T>(StructTypeNode obj, string field)
    {
        return (T?)obj.GetField(field)?.ToTypeValue(typeof(T));
    }

    /// <summary>
    /// Gets the field value from the object
    /// </summary>
    [Schema]
    public static T getfielddefault<T>(StructTypeNode obj, string field, T defaultValue)
    {
        return (T?)obj.GetField(field)?.ToTypeValue(typeof(T)) ?? defaultValue;
    }
    
    /// <summary>
    /// Gets fields from the objects in the array to a new array
    /// </summary>
    [Schema]
    public static async Task<ArrayTypeNode> getfields(SchemaContext context, ArrayTypeNode array, string field)
    {
        ArrayType arrayType = array.Type as ArrayType ?? throw new  InvalidOperationException("The array type is invalid");
        if (arrayType.ElementSchemaType is not StructType @struct) throw new InvalidOperationException("The array type is invalid");
        
        var f = @struct.Fields.FirstOrDefault(f => f.Name.Equals(field, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException($"The field {field} not found in the struct {@struct.Name}");
        if (f.TypeNode == null) throw new InvalidOperationException($"The field {field} type is null in the struct {@struct.Name}");
        AnySchemeType arrayNode = await context.GetArraySchemaTypeAsync(f.TypeNode)
                                  ?? throw new InvalidOperationException($"The field {field} type {f.Type} has no array type");

        ArrayTypeNode resultType = new (arrayNode);
        foreach (AnySchemaNode item in array)
        {
            if (item is StructTypeNode node)
            {
                AnySchemaNode? fieldNode = node.GetField(field);
                if (fieldNode != null)
                {
                    resultType.Add(fieldNode);
                }
            }
        }
        return resultType;
    }
    
    /// <summary>
    /// Sets the field and return a new json object
    /// </summary>
    [Schema]
    public static StructTypeNode setfield(StructTypeNode obj, string field, object? value)
    {
        obj[field] = value;
        return obj;
    }

    /// <summary>
    /// Sets the field and return a new json object
    /// </summary>
    [Schema]
    public static bool fieldequal<T>(StructTypeNode obj, string field, T value) where T: IComparable
    {
        AnySchemaNode? node = obj.GetField(field);
        if (node == null || node.IsEmpty) return false;
        return EqualityComparer<T>.Default.Equals(node.ToValue<T>(), value);
    }
    
    /// <summary>
    /// system.collection.notequal
    /// </summary>
    [Schema]
    public static bool fieldnotequal<T>(StructTypeNode obj, string field, T value) where T: IComparable
        => !fieldequal(obj, field, value);

    /// <summary>
    /// system.collection.greateequal
    /// </summary>
    [Schema]
    public static bool fieldgreateequal<T>(StructTypeNode obj, string field, T value) where T: IComparable
    {
        AnySchemaNode? node = obj.GetField(field);
        if (node == null || node.IsEmpty) return false;
        T? res = node.ToValue<T>();
        if (res == null) return false;
        return res.CompareTo(value) >= 0;
    }

    /// <summary>
    /// system.collection.greatethan
    /// </summary>
    [Schema]
    public static bool fieldgreatethan<T>(StructTypeNode obj, string field, T value) where T: IComparable
    {
        AnySchemaNode? node = obj.GetField(field);
        if (node == null || node.IsEmpty) return false;
        T? res = node.ToValue<T>();
        if (res == null) return false;
        return res.CompareTo(value) > 0;
    }

    /// <summary>
    /// system.collection.lessequal
    /// </summary>
    [Schema]
    public static bool fieldlessequal<T>(StructTypeNode obj, string field, T value) where T: IComparable
    {
        AnySchemaNode? node = obj.GetField(field);
        if (node == null || node.IsEmpty) return false;
        T? res = node.ToValue<T>();
        if (res == null) return false;
        return res.CompareTo(value) <= 0;
    }

    /// <summary>
    /// system.collection.lessthan
    /// </summary>
    [Schema]
    public static bool fieldlessthan<T>(StructTypeNode obj, string field, T value) where T: IComparable
    {
        AnySchemaNode? node = obj.GetField(field);
        if (node == null || node.IsEmpty) return false;
        T? res = node.ToValue<T>();
        if (res == null) return false;
        return res.CompareTo(value) < 0;
    }

    /// <summary>
    /// order by the given field
    /// </summary>
    [Schema]
    public static ArrayTypeNode orderby(ArrayTypeNode obj, string field, bool descending)
    {
        var list = new List<AnySchemaNode>(obj);
        list.Sort((a, b) =>
        {
            if (a is not StructTypeNode sa || b is not StructTypeNode sb) return 0;
            var fa = sa.GetField(field);
            var fb = sb.GetField(field);
            if ((fa == null || fa.IsEmpty) && (fb == null || fb.IsEmpty)) return 0;
            if (fa == null || fa.IsEmpty) return descending ? 1 : -1;
            if (fb == null || fb.IsEmpty) return descending ? -1 : 1;
            if (fa.Value is IComparable ca && fb.Value is IComparable cb)
                return descending ? cb.CompareTo(ca) : ca.CompareTo(cb);
            return 0;
        });
        return new ArrayTypeNode(obj.Type, list);
    }
    
    /// <summary>
    /// Skip the given count items
    /// </summary>
    [Schema]
    public static ArrayTypeNode skip(ArrayTypeNode obj, int count)
    {
        if (count <= 0) return obj;
        return count > obj.Count 
            ? new ArrayTypeNode(obj.Type) 
            : new ArrayTypeNode(obj.Type, obj.Skip(count));
    }

    /// <summary>
    /// Take the given count items
    /// </summary>
    [Schema]
    public static ArrayTypeNode take(ArrayTypeNode obj, int count)
    {
        if (count >= obj.Count) return obj;
        return count <= 0 
            ? new ArrayTypeNode(obj.Type)
            : new ArrayTypeNode(obj.Type, obj.Take(count));
    }
}