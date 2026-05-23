using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Service;
using SchemaNode.Struct;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
using NodeSchemaKind = SchemaNode.Property.Record.NodeSchemaKind;
using NodeType = SchemaNode.Runtime.NodeType;
using Object = SchemaNode.Scalar.Object;
using SchemaKind =  SchemaNode.Property.Record.SchemaKind;

namespace SchemaNode.Schema;

/// <summary>
/// The function schema
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_FUNCTION, SCHEMA_KIND_ORDER_FUNC)]
[Meta<NodeSchemaKind>(SCHEMA_KIND_FUNCTION, SCHEMA_KIND_ORDER_FUNC)]
[Meta<Property.Core.NodeType>(typeof(FunctionType))]
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
}

/// <summary>
/// Declare function property for node schema
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_NODE)]
[Relation<Visible>(NS_SYSTEM_LOGIC_EQ, $"${nameof(NodeSchema.Kind)}", SCHEMA_KIND_FUNCTION)]
public sealed class FuncProperty: Property<FunctionSchema>;

/// <summary>
/// Represents the function type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_FUNC}.type")]
public class FuncType: AnyType;

/// <summary>
/// Represents the validation function type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_FUNC}.valid")]
public class ValidFuncType: FuncType;

/// <summary>
/// Represents the union validation function type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_FUNC}.unionvalid")]
public class UnionValidFuncType : FuncType;

/**
 * The function argument information
 */
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_FUNC}.arg")]
public sealed class FuncArg
{
    /// <summary>
    /// The argument name
    /// </summary>
    [Meta<UplimitString>(PRIMARY_KEY_MAX_LEN)]
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
    [SchemaIgnore]
    public object? Default { get; set; }

    /// <summary>
    /// The schema node status
    /// </summary>
    [SchemaIgnore]
    public string? Error { get; set; }
    
    /// <summary>
    /// The argument schema type
    /// </summary>
    [SchemaIgnore]
    [JsonIgnore]
    public NodeType? NodeType  { get; set; }
}

/// <summary>
/// The function expressions
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_FUNC}.exp")]
public sealed class FuncExp {
    /// <summary>
    /// The expression name
    /// </summary>
    [Meta<UplimitString>(PRIMARY_KEY_MAX_LEN)]
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
    [SchemaIgnore]
    public string? Error { get; set; }
}

/// <summary>
/// The function call arguments
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_FUNC}.callarg")]
public class CallArg: IEquatable<CallArg>
{
    /// <summary>
    /// The argument data source, like field access path
    /// </summary>
    public string? Source { get; set; }
    
    /// <summary>
    /// The const value
    /// </summary>
    [Meta<SchemaType>(typeof(Object))]
    public JsonNode? Value { get; set; }
    
    /// <summary>
    /// The argument type
    /// </summary>
    [Meta<SchemaType>(typeof(AnyType))]
    public string? Type { get; set; }
    
    /// <summary>
    /// The node type of the call argument
    /// </summary>
    [SchemaIgnore] 
    [JsonIgnore] 
    public Runtime.ValueType? ValueType { get; set; }

    /// <summary>
    /// The data node represents the value
    /// </summary>
    [SchemaIgnore] 
    [JsonIgnore] 
    public DataNode? Constant { get; set; }

    public bool Equals(CallArg? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.IsNullOrWhiteSpace(Source)
            ? string.IsNullOrWhiteSpace(other.Source) && !Value.IsEmpty() && !other.Value.IsEmpty() && Value!.ToJsonString().Equals(other.Value!.ToJsonString())
            : !string.IsNullOrWhiteSpace(other.Source) && Source.Equals(other.Source, StringComparison.OrdinalIgnoreCase);
    }
}