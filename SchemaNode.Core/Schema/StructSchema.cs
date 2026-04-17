using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Property.Schema;
using SchemaNode.Runtime;
using SchemaNode.Scalar;
using SchemaNode.Scalar.Schema;
using SchemaNode.Struct;
using static SchemaNode.Utility.Constant;
using ValueType = SchemaNode.Property.Schema.ValueType;

namespace SchemaNode.Schema;

/// <summary>
/// The struct schema
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_STRUCT}.schema")]
[Meta<AsSchemaKind>(nameof(StructSchema), SCHEMA_KIND_ORDER_STRUCT)]
[Meta<RuntimeType>(typeof(Runtime.StructType))]
[Meta<ValueType>(typeof(StructNode))]
public sealed class StructSchema : ExtensibleSchema
{
    /// <summary>
    /// The struct fields
    /// </summary>
    public StructFieldSchema[] Fields { get; set; } = [];
    
    /// <summary>
    /// The union validations
    /// </summary>
    public StructUnionValidation[]? UnionValids { get; set; }
}

/// <summary>
/// Declare struct property for node schema
/// </summary>
[Meta<ForSchema>(nameof(NodeSchema))]
public sealed class StructProperty: Property<StructSchema>;

/// <summary>
/// The struct field schema
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_STRUCT_FIELD}.schema")]
[Meta<AsSchemaKind>(nameof(StructFieldSchema), SCHEMA_KIND_ORDER_STRUCT_FIELD)]
public sealed class StructFieldSchema : ExtensibleSchema
{
    /// <summary>
    /// The field name
    /// </summary>
    [Meta<SchemaType>(typeof(Identifier))]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// The type name of the node.
    /// </summary>
    [Meta<SchemaType>(typeof(ValueType))]
    public string Type { get; set; } = string.Empty;
}

[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_STRUCT}.unionvalid")]
public class StructUnionValidation
{
    /// <summary>
    /// The union validation func
    /// </summary>
    [Meta<SchemaType>(typeof(UnionValidFuncType))]
    public string Func { get; set; } = string.Empty;

    /// <summary>
    /// The func arguments
    /// </summary>
    public CallArg[] Args { get; set; } = [];

    /// <summary>
    /// The error message
    /// </summary>
    [NotMapped]
    public string? Error { get; set; }

    /// <summary>
    /// The function node ref
    /// </summary>
    [JsonIgnore]
    [NotMapped]
    public FunctionType? FuncNode { get; set; }
}