using SchemaNode.Attribute;
using SchemaNode.Property.Schema.Node;
using System.ComponentModel.DataAnnotations;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Schema;

[Meta<SchemaKind>(nameof(ScalarSchema))]
[Schema($"{NS_SYSTEM_SCHEMA_DEF_SCALAR}.schema")]
public sealed class ScalarSchema: ExtensibleSchema
{
    /// <summary>
    /// The base type of the scalar
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    [Meta<SchemaKind>(nameof(ScalarSchema))]
    public string? Base { get; set; }
}