using System.Collections;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using SchemaNode.Attribute;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Function;

/// <summary>
/// System.Collection Aps
/// </summary>
[SchemaNameSpace(NS_SYSTEM_COLLECTION)]
public static class SystemCollection
{
    /// <summary>
    /// Gets the array length
    /// </summary>
    [SchemaFunc]
    public static long ArrLen([SchemaFuncArg(NS_SYSTEM_ARRAY)] object array)
    {
        if (array is JsonArray jsonArray)
        {
            return jsonArray.Count;
        }
        if (array is Array arr)
        {
            return arr.LongLength;
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
    [SchemaFunc]
    public static List<T> NewArray<T>()
    {
        return new List<T>();
    }

    /// <summary>
    /// Push to the list
    /// </summary>
    [SchemaFunc]
    public static List<T> Push<T>(IEnumerable<T> arr, T value)
    {
        List<T> res = new (arr);
        res.Add(value);
        return res;
    }

    /// <summary>
    /// Combine two array and distinct
    /// </summary>
    public static List<T> Combine<T>(IEnumerable<T> left, IEnumerable<T> right)
    {
        HashSet<T> temp = [];
        List<T> res = [];
        foreach (var item in left)
        {
            if (!temp.Add(item)) continue;
            res.Add(item);
        }
        foreach (var item in right)
        {
            if (!temp.Add(item)) continue;
            res.Add(item);
        }
        return res;
    }

    [SchemaFunc]
    public static List<T> Sort<T>(IEnumerable<T> array, bool? desc) where T: IComparable
    {
        List<T> res = new (array);
        res.Sort();
        if (desc ?? false) res.Reverse();
        return res; 
    }

    /// <summary>
    /// Calc the average
    /// </summary>
    [SchemaFunc]
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
    [SchemaFunc]
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
    [SchemaFunc]
    public static JsonObject DelField(JsonObject obj, string field)
    {
        JsonObject copy = new();
        foreach (var (key, value) in obj)
        {
            if (key.Equals(field, StringComparison.OrdinalIgnoreCase)) continue;
            copy[key] = value!;
        }
        return copy;
    }

    /// <summary>
    /// Gets the field value from the object
    /// </summary>
    [SchemaFunc]
    public static T? GetField<T>(JsonObject obj, string field)
    {
        return obj.ContainsKey(field) ? (T?)typeof(T).TryConvert(obj[field]) : default(T?);
    }

    /// <summary>
    /// Gets fields from the objects in the array to a new array
    /// </summary>
    [SchemaFunc]
    public static JsonArray GetFields(JsonArray array, string field)
    {
        JsonArray copy = new();
        foreach (JsonNode? node in array)
        {
            if (node is JsonObject obj && obj.ContainsKey(field))
            {
                copy.Add(obj[field]);
            }
        }
        return copy;
    }
    
    /// <summary>
    /// Create a new json object
    /// </summary>
    [SchemaFunc]
    public static JsonObject? NewStruct() => new JsonObject();

    /// <summary>
    /// Sets the field and return a new json object
    /// </summary>
    [SchemaFunc]
    public static JsonObject SetField<T>(JsonObject obj, string field, T value)
    {
        JsonObject copy = (JsonObject)obj.DeepClone();
        copy[field] = JsonSerializer.SerializeToNode(value);
        return copy;
    }
}