using SchemaNode.Config;
using SchemaNode.Enum;

namespace SchemaNode.Schema;

/// <summary>
/// The struct schema.
/// </summary>
public class StructSchema
{
    /// <summary>
    /// The base struct type to be inherited from.
    /// </summary>
    public string? Base { get; set; }
    
    /// <summary>
    /// The struct fields
    /// </summary>
    public StructFieldConfig[] Fields { get; set; } = [];
    
    /// <summary>
    /// The relations between the fields
    /// </summary>
    public StructFieldRelation[]? Relations { get; set; }
}

/// <summary>
/// The struct field config
/// </summary>
public class StructFieldConfig: SchemaConfig
{
    /// <summary>
    /// The field name
    /// </summary>
    public string Name { get; set; }

    #region Scalar

    /// <summary>
    /// The white list
    /// </summary>
    public string[]? WhiteList { get; set; }

    /// <summary>
    /// The root value, for special scalar type values
    /// </summary>
    public string? Root { get; set; }
    
    /// <summary>
    /// The black list
    /// </summary>
    public string[]? BlackList { get; set; }

    /// <summary>
    /// The low limit of the scalar value.
    /// </summary>
    public string? LowLimit { get; set; }

    /// <summary>
    /// The up limit of the scalar value.
    /// </summary>
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
}

/// <summary>
/// The relation between fields
/// </summary>
public class StructFieldRelation
{
    /// <summary>
    /// The target field, can use . for deep fields
    /// </summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// The relation function
    /// </summary>
    public string Func { get; set; } = string.Empty;

    /// <summary>
    /// The func arguments
    /// </summary>
    public  FunctionCallArgument[] Args { get; set; } = [];

    /// <summary>
    /// The relationType type
    /// </summary>
    public RelationType Type { get; set; } = RelationType.Default;
}