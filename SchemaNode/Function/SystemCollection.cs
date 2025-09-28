using System.Collections;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using SchemaNode.Attribute;
using SchemaNode.Utility;

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
    public static long ArrLen<T>(IList array) => array.Count;

    /// <summary>
    /// Calc the average
    /// </summary>
    [SchemaFunc]
    public static T Average<T>(JsonArray array) where T : INumber<T>
    {
        T sum = T.Zero;
        foreach (JsonNode? node in array)
        {
            if (node is JsonValue val && !val.IsEmpty())
            {
                sum += T.CreateChecked(val.GetValue<T>());
            }
        }
        return sum / T.CreateChecked(array.Count);
    }

    /// <summary>
    /// Calc the average in the field
    /// </summary>
    [SchemaFunc]
    public static T AverageFields<T>(JsonArray array, string field) where T : INumber<T>
    {
        T sum = T.Zero;
        foreach (JsonNode? node in array)
        {
            if (node is JsonObject obj && obj.ContainsKey(field) && obj[field] is JsonValue val && !val.IsEmpty())
            {
                sum += T.CreateChecked(val.GetValue<T>());
            }
        }
        return sum / T.CreateChecked(array.Count);
    }
    
    /// <summary>
    /// Calc the sum
    /// </summary>
    [SchemaFunc]
    public static T Sum<T>(JsonArray array) where T : INumber<T>
    {
        T sum = T.Zero;
        foreach (JsonNode? node in array)
        {
            if (node is JsonValue val && !val.IsEmpty())
            {
                sum += T.CreateChecked(val.GetValue<T>());
            }
        }
        return sum;
    }

    /// <summary>
    /// Calc the sum in the field
    /// </summary>
    [SchemaFunc]
    public static T SumFields<T>(JsonArray array, string field) where T : INumber<T>
    {
        T sum = T.Zero;
        foreach (JsonNode? node in array)
        {
            if (node is JsonObject obj && obj.ContainsKey(field) && obj[field] is JsonValue val && !val.IsEmpty())
            {
                sum += T.CreateChecked(val.GetValue<T>());
            }
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