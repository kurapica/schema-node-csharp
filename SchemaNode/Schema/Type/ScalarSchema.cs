using SchemaNode.Attribute;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Schema;

/**
 * The schema of the scalar type
*/
[SchemaApp]
[Schema($"{NS_SYSTEM_SCHEMA_DEF_SCALAR}.schema")]
public sealed class ScalarSchema: ISchemaExtensions
{
    /// <summary>
    /// The scalar name
    /// </summary>
    [Index]
    [JsonIgnore]
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string? Name { get; set; }

    /// <summary>
    /// The base type of the scalar
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    [Schema(NS_SYSTEM_SCHEMA_TYPE_SCALAR)]
    public string? Base { get; set; }

    /// <summary>
    /// The extensions
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extensions { get; set; }

    /// <summary>
    /// Used to combine custom schema to system schema
    /// </summary>
    internal void CombineCustomSchema(ScalarSchema? other)
    {
        this.CombineExtensions(other);
    }
}
