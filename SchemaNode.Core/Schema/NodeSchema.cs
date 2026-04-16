using System.ComponentModel.DataAnnotations.Schema;
using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Property.Schema;
using SchemaNode.Scalar;
using SchemaNode.Scalar.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Schema;

/// <summary>
/// The schema container node, which can contain other nodes, such as scalar, struct, enum, array, etc.
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_NODE}.schema")]
[Meta<AsSchemaKind>(nameof(NodeSchema), SCHEMA_KIND_ORDER_NODE)]
public sealed class NodeSchema: ExtensibleSchema
{
    /// <summary>
    /// The schema name
    /// </summary>
    [Meta<UniqueIndex>(0)]
    [Meta<SchemaType>(typeof(Identifier))]
    public string Name { get; set; } = null!;
    
    /// <summary>
    /// The namespace which includes the schema
    /// </summary>
    [Meta<UniqueIndex>(1)]
    [Meta<SchemaType>(typeof(NamespaceType))]
    public string? Namespace { get; set; }
    
    /// <summary>
    /// The schema kind
    /// </summary>
    [Meta<SchemaType>(typeof(NodeSchemaKind))]
    public string Kind { get; set; } = null!;
    
    /// <summary>
    /// Used by other node schemas
    /// </summary>
    [NotMapped]
    public string[]? UsedBy { get; set; }
}