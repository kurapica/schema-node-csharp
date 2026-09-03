using SchemaNode.Node;
using SchemaNode.Runtime;
using System.Data.Common;
using System.Text.Json.Nodes;
using ValueType = SchemaNode.Runtime.ValueType;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Data;

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
    /// The scope field, used for data partition
    /// </summary>
    public bool Scope { get; init; }

    /// <summary>
    /// The target field, used for data partition
    /// </summary>
    public bool Target { get; init; }

    /// <summary>
    /// Whether the field is primary
    /// </summary>
    public bool Primary { get; init; }

    /// <summary>
    /// The max length of the string type
    /// </summary>
    public int? MaxLength { get; init; }
    
    /// <summary>
    /// The join field
    /// </summary>
    public string? JoinAppField { get; init; }
    
    /// <summary>
    /// The join data field
    /// </summary>
    public string? JoinDataField { get; init; }
    
    /// <summary>
    /// The data dict type
    /// </summary>
    public required ValueType ValueType { get; init; }
    
    /// <summary>
    /// Whether the field is used as attribute table for dynamic type
    /// </summary>
    public RelationType? RelationType { get; init; }
    
    /// <summary>
    /// The struct field relation for struct type
    /// </summary>
    public RelationType? StructRelation { get; init; }
    
    /// <summary>
    /// The relation type, either RelationType or StructRelation
    /// </summary>
    public bool HasTypeRelation => RelationType != null || StructRelation != null;

    /// <summary>
    /// The field is a value field if it has no type relation, and is not scope or target field
    /// </summary>
    public bool IsValueField => !HasTypeRelation && !IsKeyField && !IsJoinField;
    
    /// <summary>
    /// The field is a key field if it is primary, or scope or target field
    /// </summary>
    public bool IsKeyField => Primary || Scope;
    
    /// <summary>
    /// The field is a join field if it has JoinAppField
    /// </summary>
    public bool IsJoinField => !string.IsNullOrWhiteSpace(JoinAppField);

    /// <summary>
    /// Get JToken from reader
    /// </summary>
    public IValueAccess? FromReader(DbDataReader reader, int col = 0)
    {
        if (reader.IsDBNull(col)) return null;
        object? data;
        if (Type == DynamicTableFieldType.Json)
        {
            object raw = reader.GetValue(col);

            JsonNode? json = raw is DBNull ? null : raw switch
            {
                string s => JsonNode.Parse(s),
                byte[] b => JsonNode.Parse(b),
                _ => null
            };

            data = json;
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

        return ValueType.From(data);
    }

    /// <summary>
    /// Gets the string of the JToken value
    /// </summary>
    public string? ToString(IValueAccess? value)
    {
        if (value == null || value.IsEmpty) return null;

        return Type switch
        {
            DynamicTableFieldType.Bool => value.GetValue<bool>() ? "1" : "0",
            DynamicTableFieldType.DateTime => value.GetValue<DateTimeOffset>().ToString("yyyy-MM-dd HH:mm:ss"),
            _ => value.GetValue<object>()?.ToString()
        };
    }
}
