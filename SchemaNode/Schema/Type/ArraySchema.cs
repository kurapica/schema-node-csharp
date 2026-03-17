using SchemaNode.Attribute;
using SchemaNode.Enum;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Schema;

/// <summary>
/// The array schema
/// </summary>
[SchemaApp]
[Schema($"{NS_SYSTEM_SCHEMA_DEF_ARRAY}.schema")]
public sealed class ArraySchema
{
    /// <summary>
    /// The array name
    /// </summary>
    [Index]
    [JsonIgnore]
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string? Name { get; set; }

    /// <summary>
    /// The element type of the array.
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    [Schema(NS_SYSTEM_SCHEMA_TYPE_RULE_ARELE)]
    public string? Element { get; set; }

    /// <summary>
    /// Whether the array should be treated as a whole value,
    /// no element schema nodes would be created
    /// </summary>
    public bool? Single { get; set; }

    /// <summary>
    /// The primary fields of the array if the element is a struct.
    /// </summary>
    public string[]? Primary { get; set; }

    /// <summary>
    /// The indexes
    /// </summary>
    public DataIndex[]? Indexes { get; set; }

    /// <summary>
    /// The data combine rule
    /// </summary>
    public DataCombine[]? Combines { get; set; }

    /// <summary>
    /// The realtions between the fields
    /// </summary>
    public StructRelationSchema[]? Relations { get; set; }
    
    /// <summary>
    /// The additional data
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }


    /// <summary>
    /// Used to combine custom schema to system schema
    /// </summary>
    internal void CombineCustomSchema(ArraySchema? other)
    {
        Single = other?.Single ?? Single;
        Combines = other?.Combines ?? Combines;
        Relations = other?.Relations ?? Relations;
    }

}

/// <summary>
/// The data combine settings
/// </summary>
[Schema($"{NS_SYSTEM_SCHEMA_DEF_ARRAY}.{nameof(DataCombine)}")]
public sealed class DataCombine
{
    /// <summary>
    /// The field
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// The combine type
    /// </summary>
    public DataCombineType Type { get; set; } = DataCombineType.Assign;
}

[Schema($"{NS_SYSTEM_SCHEMA_DEF_ARRAY}.{nameof(DataIndex)}")]
public sealed class DataIndex
{
    /// <summary>
    /// The index name
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The index fields
    /// </summary>
    public string[] Fields { get; set; } = [];
}