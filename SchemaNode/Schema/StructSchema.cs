using System.Text.Json;
using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Runtime;
using System.ComponentModel.DataAnnotations;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Schema;

/// <summary>
/// The struct schema.
/// </summary>
[SchemaStruct([nameof(Name)])]
[SchemaApp]
public class StructSchema
{
    /// <summary>
    /// The struct name
    /// </summary>
    [JsonIgnore]
    [MaxLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string? Name { get; set; }
    
    /// <summary>
    /// The base struct type to be inherited from.
    /// </summary>
    [MaxLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string? Base { get; set; }
    
    /// <summary>
    /// The struct fields
    /// </summary>
    public StructFieldConfig[] Fields { get; set; } = [];
    
    /// <summary>
    /// The relations between the fields
    /// </summary>
    public StructFieldRelation[]? Relations { get; set; }
    
    /// <summary>
    /// The additional data
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
}

/// <summary>
/// The struct field config
/// </summary>
public class StructFieldConfig
{
    /// <summary>
    /// The field name
    /// </summary>
    [MaxLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// The type name of the node.
    /// </summary>
    [MaxLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// The label of the node.
    /// </summary>
    public LocaleString? Display { get; set; }

    /// <summary>
    /// The description of the node.
    /// </summary>
    public LocaleString? Desc { get; set; }
    
    /// <summary>
    /// The error message if validation failed.
    /// </summary>
    public LocaleString? Error { get; set; }

    /// <summary>
    /// The node data is required.
    /// </summary>
    public bool? Require { get; set; }

    /// <summary>
    /// The node data is immutable, un-changeable if init-ed.
    /// </summary>
    public bool? Immutable { get; set; }

    /// <summary>
    /// The node data is readonly.
    /// </summary>
    public bool? Readonly { get; set; }

    /// <summary>
    /// The node should be invisible.
    /// </summary>
    public bool? Invisible { get; set; }

    /// <summary>
    /// The node should be display only, won't be submitted.
    /// </summary>
    public bool? DisplayOnly { get; set; }

    /// <summary>
    /// The unit of the node data like 'm/s', '%', '°C'.
    /// </summary>
    public LocaleString? Unit { get; set; }

    /// <summary>
    /// The default value of the node.
    /// </summary>
    [MaxLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string? Default { get; set; }
    
    /// <summary>
    /// The additional data
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
    
    #region Scalar

    /// <summary>
    /// The root value, for special scalar type values
    /// </summary>
    [MaxLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string? Root { get; set; }
    
    /// <summary>
    /// The white list
    /// </summary>
    public string[]? WhiteList { get; set; }
    
    /// <summary>
    /// The black list
    /// </summary>
    public string[]? BlackList { get; set; }

    /// <summary>
    /// The low limit of the scalar value.
    /// </summary>
    [MaxLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string? LowLimit { get; set; }

    /// <summary>
    /// The up limit of the scalar value.
    /// </summary>
    [MaxLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string? UpLimit { get; set; }

    /// <summary>
    /// The enum white list only used for suggest.
    /// </summary>
    public bool? AsSuggest { get; set; }

    /// <summary>
    /// When calculating the up limit, use the original value.
    /// </summary>
    public bool? UseOriginForUpLimit { get; set; }
    
    #endregion
    
    #region Enum
    
    /// <summary>
    /// The enum cascade limit.
    /// </summary>
    public int? Cascade { get; set; }

    /// <summary>
    /// Allow use enum value in any level.
    /// </summary>
    public bool? AnyLevel { get; set; }

    /// <summary>
    /// Don't allow flags enum value combination.
    /// </summary>
    public bool? SingleFlag { get; set; }
    
    #endregion

    #region Array
    
    /// <summary>
    /// The array data is increase update, only usable within application
    /// </summary>
    public bool? IncrUpdate { get; set; }

    /// <summary>
    /// The page count
    /// </summary>
    public int? Count { get; set; }
    
    /// <summary>
    /// The query offset
    /// </summary>
    public int? Offset { get; set; }

    /// <summary>
    /// The data total count
    /// </summary>
    public int? Total { get; set; }

    /// <summary>
    /// Use descend order
    /// </summary>
    public bool? Descend { get; set; }

    #endregion
    
    #region Ref
    
    /// <summary>
    /// The type node ref
    /// </summary>
    [JsonIgnore]
    [SchemaStructMemIgnore]
    public AnySchemeType? TypeNode { get; set; }
    
    #endregion
}

/// <summary>
/// The relation between fields
/// </summary>
public class StructFieldRelation
{
    /// <summary>
    /// The target field, can use . for deep fields
    /// </summary>
    [MaxLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// The relation function
    /// </summary>
    [MaxLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string Func { get; set; } = string.Empty;

    /// <summary>
    /// The func arguments
    /// </summary>
    public  FunctionCallArgument[] Args { get; set; } = [];

    /// <summary>
    /// The relationType type
    /// </summary>
    public RelationType Type { get; set; } = RelationType.Default;
    
    /// <summary>
    /// The function node ref
    /// </summary>
    [JsonIgnore]
    [SchemaStructMemIgnore]
    public FunctionType? FuncNode { get; set; }
}