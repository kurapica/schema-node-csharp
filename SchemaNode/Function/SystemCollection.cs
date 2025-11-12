using System.Collections;
using System.Numerics;
using System.Text.Json.Nodes;
using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Function;

/// <summary>
/// System.Collection Aps
/// </summary>
[SchemaType(NS_SYSTEM_COLLECTION)]
public static class SystemCollection
{
    /// <summary>
    /// Gets the array length
    /// </summary>
    [SchemaType]
    public static long ArrLen([SchemaType(NS_SYSTEM_ARRAY)] object array)
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
    [SchemaType]
    public static List<T> NewArray<T>()
    {
        return new List<T>();
    }

    /// <summary>
    /// Push to the list
    /// </summary>
    [SchemaType]
    public static List<T> Push<T>(IEnumerable<T> arr, T value)
    {
        List<T> res = new (arr);
        res.Add(value);
        return res;
    }

    /// <summary>
    /// Whether the list contains the item
    /// </summary>
    [SchemaType]
    public static bool Contains<T>(IEnumerable<T> array, T value) where T: IComparable
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
    [SchemaType]
    public static bool NotContains<T>(IEnumerable<T> array, T value) where T: IComparable
    {
        return !Contains(array, value);
    }

    /// <summary>
    /// Calc the average
    /// </summary>
    [SchemaType]
    public static T Average<T>(IEnumerable<T> array) where T : INumber<T>
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
    [SchemaType]
    public static T Sum<T>(IEnumerable<T> array) where T : INumber<T>
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
    [SchemaType]
    public static StructTypeNode DelField(StructTypeNode obj, string field)
    {
        obj[field] = null;
        return obj;
    }

    /// <summary>
    /// Whether the object has the field
    /// </summary>
    [SchemaType]
    public static bool ContainsKey(StructTypeNode obj, string field)
    {
        return obj[field] != null;
    }

    /// <summary>
    /// Whether the object not has the field
    /// </summary>
    [SchemaType]
    public static bool NotContainsKey(StructTypeNode obj, string field)
    {
        return obj[field] == null;
    }

    /// <summary>
    /// Gets the field value from the object
    /// </summary>
    [SchemaType]
    public static T? GetField<T>(StructTypeNode obj, string field)
    {
        return (T?)(obj.GetField(field)?.ToTypeValue(typeof(T)));
    }

    /// <summary>
    /// Gets fields from the objects in the array to a new array
    /// </summary>
    [SchemaType]
    public static async Task<ArrayTypeNode> GetFields(SchemaContext context, ArrayTypeNode array, string field)
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
    [SchemaType]
    public static StructTypeNode SetField(StructTypeNode obj, string field, object? value)
    {
        obj[field] = value;
        return obj;
    }

    /// <summary>
    /// Sets the field and return a new json object
    /// </summary>
    [SchemaType]
    public static bool FieldEqual<T>(StructTypeNode obj, string field, T value) where T: IComparable
    {
        AnySchemaNode? node = obj.GetField(field);
        if (node == null || node.IsEmpty) return false;

        return EqualityComparer<T>.Default.Equals(node.ToValue<T>(), value);
    }
}