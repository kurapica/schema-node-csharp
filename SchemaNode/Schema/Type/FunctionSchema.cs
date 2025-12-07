using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Runtime;
using System.ComponentModel.DataAnnotations;
using static SchemaNode.Utility.Constant;
using System.ComponentModel.DataAnnotations.Schema;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Schema;

/// <summary>
/// The schema of function
/// </summary>
[SchemaApp]
public class FunctionSchema
{
    /// <summary>
    /// The function name
    /// </summary>
    [Index]
    [JsonIgnore]
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string? Name { get; set; }
    
    /// <summary>
    /// The return type of the function, T T1 T2 means the generic type
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
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

    /// <summary>
    /// Call server if server provided
    /// </summary>
    public bool? Server  { get; set; }

    /// <summary>
    /// The client should not cache the result
    /// </summary>
    public bool? Nocache  { get; set; }
}

/**
 * The function argument information
 */
public class FuncArg
{
    /// <summary>
    /// The argument name
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The argument type, T T1 T2 means the generic type
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Whether the argument is nullable
    /// </summary>
    public bool? Nullable { get; set; }

    /// <summary>
    /// The argument is params
    /// </summary>
    public bool? Params { get; set; }
    
    /// <summary>
    /// The schema node status
    /// </summary>
    [NotMapped]
    public SchemaNodeStatus? Status { get; set; }
}

/// <summary>
/// The function expressions
/// </summary>
public class FuncExp {
    /// <summary>
    /// The expression name
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The call function
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    [Schema(NS_SYSTEM_SCHEMA_FUNC_TYPE)]
    public string Func { get; set; } = string.Empty;

    /// <summary>
    /// The calling type
    /// </summary>
    public ExpressionType Type { get; set; } = ExpressionType.Call;
     
    /// <summary>
    /// The expression type
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string Return { get; set; } = string.Empty;

    /// <summary>
    /// The argument list, should be exp name or argument name.
    /// </summary>
    public FuncCallArg[] Args { get; set; } = [];
    
    /// <summary>
    /// The schema node status
    /// </summary>
    [NotMapped]
    public SchemaNodeStatus? Status { get; set; }
}
  
/// <summary>
/// The function call argument
/// </summary>
public class FuncCallArg {
    /// <summary>
    /// The argument name or expression name
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string? Name { get; set; }

    /// <summary>
    /// The const value
    /// </summary>
    public JsonNode? Value { get; set; }
    
    /// <summary>
    /// The given exp type if function can't infer the type
    /// </summary>
    public string? Type { get; set; }
    
    /// <summary>
    /// The value type
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    public AnySchemeType? TypeNode { get; set; }
}