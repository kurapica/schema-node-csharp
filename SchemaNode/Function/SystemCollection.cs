using System.Collections;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using SchemaNode.Attribute;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Function;

/// <summary>
/// System.Collection Aps
/// </summary>
[SchemaNameSpace("system.collection")]
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
    public static JsonNode? GetField(JsonObject obj, string field) => obj[field];

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
    public static JsonObject SetField(JsonObject obj, string field, object value)
    {
        JsonObject copy = (JsonObject)obj.DeepClone();
        copy[field] = JsonSerializer.SerializeToNode(value);
        return copy;
    }
}