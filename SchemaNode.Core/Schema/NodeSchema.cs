using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Property;
using SchemaNode.Property.Presentation;
using SchemaNode.Property.Schema;
using SchemaNode.Scalar;
using SchemaNode.Scalar.Schema;
using SchemaNode.Struct;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
using NodeSchemaKind = SchemaNode.Enum.NodeSchemaKind;
// ReSharper disable NotAccessedPositionalProperty.Global

namespace SchemaNode.Schema;

/// <summary>
/// The schema container node, which can contain other nodes, such as scalar, struct, enum, array, etc.
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_NODE}.schema")]
[Meta<SchemaKind>(SCHEMA_KIND_NODE, SCHEMA_KIND_ORDER_NODE)]
public sealed class NodeSchema: ExtensibleSchema
{
    /// <summary>
    /// The schema name
    /// </summary>
    [Meta<PrimaryIndex>(1)]
    [Meta<SchemaType>(typeof(Identifier))]
    public string Name { get; set; } = null!;
    
    /// <summary>
    /// The namespace which includes the schema
    /// </summary>
    [Meta<PrimaryIndex>(0)]
    [Meta<SchemaType>(typeof(NamespaceType))]
    public string? Namespace { get; set; }
    
    [SchemaIgnore]
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
    [SchemaIgnore]
    public NodeSchema[]? Schemas { get; set; }
    
    /// <summary>
    /// The compatible types
    /// </summary>
    [SchemaIgnore]
    public CompatibleSchema[]? Compatibles { get; set; }
    
    /// <summary>
    /// Used by other node schemas
    /// </summary>
    [SchemaIgnore]
    public string[]? UsedBy { get; set; }
    
    /// <summary>
    /// The C# type of the schema
    /// </summary>
    [SchemaIgnore]
    [JsonIgnore]
    public Type? Type { get; internal set; }
    
    /// <summary>
    /// The C# equivalent types
    /// </summary>
    [SchemaIgnore]
    [JsonIgnore]
    public Type[]? Equivalents { get; internal set; }
    
    // Create Node Schema
    internal static NodeSchema Create(string kind, string name, Type? type = null, string? display = null)
    {
        NodeSchema nodeSchema = new()
        {
            Name = name.GetSchemaName(),
            Namespace = name.GetNamespace(),
            Kind = kind,
            Type = type
        };
        nodeSchema.SetProperty<Display, LocaleString>(display ?? type?.GetSummaryFromXmlDoc() ?? name);
        if (type is not null)
            foreach (IProperty prop in type.GetMetaPropertiesForSchema<IProperty>(SCHEMA_KIND_NODE))
                nodeSchema.SetProperty(prop);
        return nodeSchema;
    }

    internal static NodeSchema Create(string kind, string @namespace, string name, Type? type = null, string? display = null)
    {
        return Create(kind, $"{@namespace}.{name}".Trim('.'), type, display);
    }
}


/// <summary>
/// The compatible schema record
/// </summary>
/// <param name="To">The compatible type</param>
/// <param name="Convert">The convert function</param>
public sealed record CompatibleSchema(string To, string Convert);