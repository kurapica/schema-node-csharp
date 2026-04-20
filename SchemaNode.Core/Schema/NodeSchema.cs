using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Property.Schema;
using SchemaNode.Scalar;
using SchemaNode.Scalar.Schema;
using static SchemaNode.Utility.Constant;
using NodeSchemaKind = SchemaNode.Enum.NodeSchemaKind;

namespace SchemaNode.Schema;

/// <summary>
/// The schema container node, which can contain other nodes, such as scalar, struct, enum, array, etc.
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_NODE}.schema")]
[Meta<SchemaKind>(nameof(NodeSchema), SCHEMA_KIND_ORDER_NODE)]
public sealed class NodeSchema: ExtensibleSchema
{
    /// <summary>
    /// The schema name
    /// </summary>
    [Meta<UniqueIndex>(1)]
    [Meta<SchemaType>(typeof(Identifier))]
    public string Name { get; set; } = null!;
    
    /// <summary>
    /// The namespace which includes the schema
    /// </summary>
    [Meta<UniqueIndex>(0)]
    [Meta<SchemaType>(typeof(NamespaceType))]
    public string? Namespace { get; set; }
    
    [NotMapped]
    [JsonIgnore]
    public string FullName => $"{Namespace}.{Name}".Trim('.');
    
    /// <summary>
    /// The schema kind
    /// </summary>
    [Meta<SchemaType>(typeof(NodeSchemaKind))]
    public string Kind { get; set; } = null!;
    
    /// <summary>
    /// The schema is system defined, can't be change
    /// </summary>
    public bool IsSystem { get; set; }
    
    /// <summary>
    /// The sub schemas (for namespace schemas)
    /// </summary>
    [NotMapped]
    public NodeSchema[]? Schemas { get; set; }
    
    /// <summary>
    /// The compatible types
    /// </summary>
    [NotMapped]
    public CompatibleSchema[]? Compatibles { get; set; }
    
    /// <summary>
    /// Used by other node schemas
    /// </summary>
    [NotMapped]
    public string[]? UsedBy { get; set; }
    
    /// <summary>
    /// The C# type of the schema
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    public Type? Type { get; internal set; }
    
    /// <summary>
    /// The C# equivalent types
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    public Type[]? Equivalents { get; internal set; }
}


/// <summary>
/// The compatible schema record
/// </summary>
/// <param name="To">The compatible type</param>
/// <param name="Convert">The convert function</param>
public sealed record CompatibleSchema(string To, string Convert);