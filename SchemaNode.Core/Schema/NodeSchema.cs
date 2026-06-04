using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Function;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Scalar;
using SchemaNode.Struct;
using SchemaNode.Utility;
using System.Text.Json.Serialization;
using static SchemaNode.Utility.Constant;
using SchemaKind =  SchemaNode.Property.Record.SchemaKind;
using String = SchemaNode.Scalar.String;
using Type = System.Type;
// ReSharper disable NotAccessedPositionalProperty.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Schema;

/// <summary>
/// The schema container node, which can contain other nodes, such as scalar, struct, enum, array, etc.
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_NODE, SCHEMA_KIND_ORDER_NODE)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_NODE}.schema")]
[Meta<Attach>(SCHEMA_KIND_NODE)]
public sealed class NodeSchema: ExtensibleSchema
{
    /// <summary>
    /// The namespace which includes the schema
    /// </summary>
    [Meta<PrimaryIndex>(0)]
    [Meta<SchemaType>(typeof(NamespaceType))]
    public string? Namespace { get; set; }
    
    /// <summary>
    /// The schema name
    /// </summary>
    [Meta<PrimaryIndex>(1)]
    [Meta<SchemaType>(typeof(Identifier))]
    public string Name { get; set; } = null!;
    
    /// <summary>
    /// The full namespace name
    /// </summary>
    [SchemaIgnore]
    [JsonIgnore]
    public string FullName => $"{Namespace}.{Name}".Trim('.');
    
    /// <summary>
    /// The schema kind
    /// </summary>
    [Meta<SchemaType>(typeof(NodeSchemaKind))]
    public string Kind { get; set; } = null!;
   
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
    public IReadOnlyList<Type>? Equivalents { get; internal set; }
    
    /// <summary>
    /// The schema provider
    /// </summary>
    [SchemaIgnore]
    [JsonIgnore]
    internal Type? Provider { get; set; }
    
    /// <summary>
    /// The schema load state
    /// </summary>
    [SchemaIgnore]
    [JsonIgnore]
    internal SchemaLoadState? LoadState { get; set; }
    
    /// <summary>
    /// Gets the clone
    /// </summary>
    public NodeSchema Clone(ISchemaRuntime? runtime = null)
    {
        var nodeSchema = new NodeSchema()
        {
            Name = Name,
            Namespace = Namespace,
            Kind = Kind,
        };
        nodeSchema.CombineExtensions(this, runtime);
        return nodeSchema;
    }
    
    // Create Node Schema with full name
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

    // Create Node Schema with split namespace and name
    internal static NodeSchema Create(string kind, string @namespace, string name, Type? type = null, string? display = null)
        => Create(kind, $"{@namespace}.{name}".Trim('.'), type, display);
}

/// <summary>
/// Represents the namespace type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_NODE}.type")]
[Meta<UplimitString>(PRIMARY_KEY_MAX_LEN)]
[Meta<EntrySource>($"{NS_SYSTEM_SCHEMA_REFLECT}.{nameof(SystemReflect.gettypes)}", NODE_SELF)]
public class AnyType: String;

/// <summary>
/// Represents the value type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_NODE}.valuetype")]
[Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_IS_VALUE_KIND, NODE_SELF)]
public class ValueType : AnyType;

/// <summary>
/// The compatible schema record
/// </summary>
/// <param name="To">The compatible type</param>
/// <param name="Convert">The convert function</param>
public sealed record CompatibleSchema(string To, string Convert);

public static class NodeSchemaExtensions
{
    /// <summary>
    /// Loading properties for node schemas
    /// </summary>
    public static async Task<(IProperty[] Properties, Runtime.NodeType[] RefTypes, string? Error)>
        LoadNodeSchemaProperties(this ExtensibleSchema schema, SchemaContext context, string? kind = null)
    {
        kind ??= schema.GetType().GetMetaProperty<SchemaKind>()?.Value;
        if (string.IsNullOrWhiteSpace(kind)) return (Array.Empty<IProperty>(), Array.Empty<Runtime.NodeType>(), ErrorCodes.NO_DEFINITION);

        var properties = schema.GetProperties(context.Runtime.GetSchemaKindProperties(kind));
    }
}