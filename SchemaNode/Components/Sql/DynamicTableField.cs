using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Data.Common;
using System.Text.Json.Nodes;

namespace SchemaNode.Components;

/// <summary>
/// The dynamic table field info
/// </summary>
public class DynamicTableField
{
    /// <summary>
    /// The field name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The dynamic field type
    /// </summary>
    public DynamicTableFieldType Type { get; init; }

    /// <summary>
    /// The complex field info
    /// </summary>
    public DataFieldComplexInfo? Complex { get; init; }

    /// <summary>
    /// Whether the field is primary
    /// </summary>
    public bool Primary { get; init; }

    /// <summary>
    /// The max length of the string type
    /// </summary>
    public int? MaxLength { get; init; }

    /// <summary>
    /// The data dict type
    /// </summary>
    public required AnySchemeType SchemaType { get; init; }

    /// <summary>
    /// The struct field node of primary field
    /// </summary>
    public StructFieldConfig? StructFieldNode { get; init; }

    /// <summary>
    /// Whether the type is string data
    /// </summary>
    public bool IsString => Type switch
    {
        DynamicTableFieldType.Bool => false,
        DynamicTableFieldType.Smallint => false,
        DynamicTableFieldType.USmallint => false,
        DynamicTableFieldType.Mediumint => false,
        DynamicTableFieldType.UMediumint => false,
        DynamicTableFieldType.Int => false,
        DynamicTableFieldType.UInt => false,
        DynamicTableFieldType.BigInt => false,
        DynamicTableFieldType.UBigInt => false,
        DynamicTableFieldType.Float => false,
        DynamicTableFieldType.Double => false,
        _ => true
    };

    /// <summary>
    /// Get JToken from reader
    /// </summary>
    public AnySchemaNode? FromReader(DbDataReader reader, int col = 0)
    {
        if (reader.IsDBNull(col)) return null;
        object? data;
        if (Type == DynamicTableFieldType.Json)
        {
            data = JsonNode.Parse(reader.GetString(col));
        }
        else
        {
            data = (Type switch
            {
                DynamicTableFieldType.Bool => reader.GetByte(col) == 1,
                DynamicTableFieldType.Smallint => reader.GetInt16(col),
                DynamicTableFieldType.USmallint => reader.GetInt32(col),
                DynamicTableFieldType.Mediumint => reader.GetInt32(col),
                DynamicTableFieldType.UMediumint => reader.GetInt32(col),
                DynamicTableFieldType.Int => reader.GetInt32(col),
                DynamicTableFieldType.UInt => reader.GetInt64(col),
                DynamicTableFieldType.BigInt => reader.GetInt64(col),
                DynamicTableFieldType.UBigInt => reader.GetInt64(col),
                DynamicTableFieldType.Float => reader.GetFloat(col),
                DynamicTableFieldType.Double => reader.GetDouble(col),
                DynamicTableFieldType.DateTime => reader.GetDateTime(col),
                _ => reader.GetString(col)
            });
        }

        return SchemaType.CreateNode(data);
    }

    /// <summary>
    /// Gets the string of the JToken value
    /// </summary>
    public string? ToString(AnySchemaNode? value)
    {
        if (value == null || value.IsEmpty) return null;

        return Type switch
        {
            DynamicTableFieldType.Bool => value.ToValue<bool>() ? "1" : "0",
            DynamicTableFieldType.DateTime => value.ToValue<DateTime>().ToString("yyyy-MM-dd HH:mm:ss"),
            _ => value.ToString()
        };
    }

    /// <summary>
    /// Gets the string of the JToken value
    /// </summary>
    public string? ToString(object? value)
    {
        if (value == null) return null;

        return Type switch
        {
            DynamicTableFieldType.Bool => Convert.ToBoolean(value) ? "1" : "0",
            DynamicTableFieldType.DateTime => Convert.ToDateTime(value).ToString("yyyy-MM-dd HH:mm:ss"),
            _ => value.ToString()
        };
    }

    /// <summary>
    /// Gets the string of the JToken value
    /// </summary>
    public string? ToString(JsonNode? value)
    {
        if (value == null && value.IsEmpty()) return null;
        JsonNode v = value!;
        return Type switch
        {
            DynamicTableFieldType.Bool => v.GetValue<bool>() ? "1" : "0",
            DynamicTableFieldType.DateTime => v.GetValue<DateTime>().ToString("yyyy-MM-dd HH:mm:ss"),
            DynamicTableFieldType.Json => v.ToJsonString(),
            _ => v.ToValue<string>()
        };
    }
}
