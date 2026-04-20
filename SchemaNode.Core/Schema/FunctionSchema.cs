using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Generator;
using SchemaNode.Property;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Presentation;
using SchemaNode.Property.Schema;
using SchemaNode.Runtime;
using SchemaNode.Scalar.Schema;
using SchemaNode.Struct;
using static SchemaNode.Utility.Constant;
using ValueType = SchemaNode.Scalar.Schema.ValueType;

namespace SchemaNode.Schema;

/// <summary>
/// The function schema
/// </summary>
[Meta<SchemaKind>("function", SCHEMA_KIND_ORDER_FUNC)]
[Meta<NodeSchemaType>(typeof(FunctionType))]
[Meta<SchemaGenerator>(typeof(FunctionGenerator))]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_FUNC}.schema")]
public sealed class FunctionSchema: ExtensibleSchema
{
    /// <summary>
    /// The return type of the function, T T1 T2 means the generic type
    /// </summary>
    [Meta<SchemaType>(typeof(ValueType))]
    public string Return { get; set; } = string.Empty;

    /// <summary>
    /// The function arguments
    /// </summary>
    public FuncArg[] Args { get; set; } = [];

    /// <summary>
    /// The function expressions
    /// </summary>
    public FuncExp[] Exps { get; set; } = [];

    /// <summary>
    /// The basic type of generic types, provided to T(single generic type),
    /// T1, T2(for multi generic type)
    /// </summary>
    public string[]? Generic { get; set; }
}

/// <summary>
/// Declare function property for node schema
/// </summary>
[Meta<ForSchema>(nameof(NodeSchema))]
[Relation<Visible>(NS_SYSTEM_LOGIC_EQ, $"${nameof(NodeSchema.Kind)}", "function")]
public sealed class FuncProperty: Property<FunctionSchema>;

/**
 * The function argument information
 */
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_FUNC}.arg")]
public sealed class FuncArg
{
    /// <summary>
    /// The argument name
    /// </summary>
    [Meta<UplimitStringProperty>(PRIMARY_KEY_MAX_LEN)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The argument type, T T1 T2 means the generic type
    /// </summary>
    [Meta<SchemaType>(typeof(ValueType))]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Whether the argument is nullable
    /// </summary>
    public bool? Nullable { get; set; }
    
    /// <summary>
    /// The argument display name
    /// </summary>
    public LocaleString? Display { get; set; }

    /// <summary>
    /// The argument is params
    /// </summary>
    public bool? Params { get; set; }

    /// <summary>
    /// The default value
    /// </summary>
    [NotMapped]
    public object? Default { get; set; }

    /// <summary>
    /// The schema node status
    /// </summary>
    [NotMapped]
    public string? Error { get; set; }
    
    /// <summary>
    /// The argument schema type
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    public AnySchemaType? SchemaType  { get; set; }
}

/// <summary>
/// The function expressions
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_FUNC}.exp")]
public sealed class FuncExp {
    /// <summary>
    /// The expression name
    /// </summary>
    [Meta<UplimitStringProperty>(PRIMARY_KEY_MAX_LEN)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The call function
    /// </summary>
    [Meta<SchemaType>(typeof(FuncType))]
    public string Func { get; set; } = string.Empty;

    /// <summary>
    /// The calling type
    /// </summary>
    public ExpType Type { get; set; } = ExpType.Call;
     
    /// <summary>
    /// The expression type
    /// </summary>
    [Meta<SchemaType>(typeof(ValueType))]
    public string Return { get; set; } = string.Empty;

    /// <summary>
    /// The argument list, should be exp name or argument name.
    /// </summary>
    public CallArg[] Args { get; set; } = [];
    
    /// <summary>
    /// The error message
    /// </summary>
    [NotMapped]
    public string? Error { get; set; }
}

/// <summary>
/// The function call arguments
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_FUNC}.callarg")]
public class CallArg
{
    /// <summary>
    /// The argument data source, like field access path
    /// </summary>
    public string? Source { get; set; }
    
    /// <summary>
    /// The const value
    /// </summary>
    [Meta<SchemaType>(typeof(AnyValue))]
    public JsonNode? Value { get; set; }
    
    /// <summary>
    /// The argument type
    /// </summary>
    [Meta<SchemaType>(typeof(AnyType))]
    public string? Type { get; set; }
}