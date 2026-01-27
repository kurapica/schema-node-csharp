using System.Text.Json;
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
/// The struct schema.
/// </summary>
[SchemaApp]
public class StructSchema
{
    /// <summary>
    /// The struct name
    /// </summary>
    [Index]
    [JsonIgnore]
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string? Name { get; set; }
    
    /// <summary>
    /// The base struct type to be inherited from.
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    [Schema(NS_SYSTEM_SCHEMA_STRUCT_TYPE)]
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
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// The type name of the node.
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    [Schema(NS_SYSTEM_SCHEMA_VALUE_TYPE)]
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
    /// The struct field flags
    /// </summary>
    public StructFieldFlags Flags { get; set; } = StructFieldFlags.None;

    /// <summary>
    /// The node data is required.
    /// </summary>
    [NotMapped]
    public bool? Require
    {
        get => (Flags & StructFieldFlags.Require) != 0 ? true : null;
        set
        {
            if (value == true)
            {
                Flags |= StructFieldFlags.Require;
            }
            else
            {
                Flags &= ~StructFieldFlags.Require;
            }
        }
    }

    /// <summary>
    /// The node data is immutable, un-changeable if init-ed.
    /// </summary>
    [NotMapped]
    public bool? Immutable
    {
        get => (Flags & StructFieldFlags.Immutable) != 0 ? true : null;
        set
        {
            if (value == true)
            {
                Flags |= StructFieldFlags.Immutable;
            }
            else
            {
                Flags &= ~StructFieldFlags.Immutable;
            }
        }
    }

    /// <summary>
    /// The node data is readonly.
    /// </summary>
    [NotMapped]
    public bool? Readonly
    {
        get => (Flags & StructFieldFlags.Readonly) != 0 ? true : null;
        set
        {
            if (value == true)
            {
                Flags |= StructFieldFlags.Readonly;
            }
            else
            {
                Flags &= ~StructFieldFlags.Readonly;
            }
        }
    }

    /// <summary>
    /// The node should be invisible.
    /// </summary>
    [NotMapped]
    public bool? Invisible
    {
        get => (Flags & StructFieldFlags.Invisible) != 0 ? true : null;
        set
        {
            if (value == true)
            {
                Flags |= StructFieldFlags.Invisible;
            }
            else
            {
                Flags &= ~StructFieldFlags.Invisible;
            }
        }
    }

    /// <summary>
    /// The node should be display only, won't be submitted.
    /// </summary>
    [NotMapped]
    public bool? DisplayOnly
    {
        get => (Flags & StructFieldFlags.DisplayOnly) != 0 ? true : null;
        set
        {
            if (value == true)
            {
                Flags |= StructFieldFlags.DisplayOnly;
            }
            else
            {
                Flags &= ~StructFieldFlags.DisplayOnly;
            }
        }
    }
    
    /// <summary>
    /// Upack/pack additional data for the json node.
    /// </summary>
    [NotMapped]
    public bool? Unpack
    {
        get => (Flags & StructFieldFlags.Unpack) != 0 ? true : null;
        set
        {
            if (value == true)
            {
                Flags |= StructFieldFlags.Unpack;
            }
            else
            {
                Flags &= ~StructFieldFlags.Unpack;
            }
        }
    }

    /// <summary>
    /// The unit of the node data like 'm/s', '%', '°C'.
    /// </summary>
    public LocaleString? Unit { get; set; }

    /// <summary>
    /// The default value of the node.
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string? Default { get; set; }
    
    /// <summary>
    /// The additional data
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
    
    /// <summary>
    /// The schema node status
    /// </summary>
    [NotMapped]
    public SchemaNodeStatus? Status { get; set; }

    #region Scalar

    /// <summary>
    /// The root value, for special scalar type values
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
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
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string? LowLimit { get; set; }

    /// <summary>
    /// The up limit of the scalar value.
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string? UpLimit { get; set; }

    /// <summary>
    /// The enum white list only used for suggest.
    /// </summary>
    [NotMapped]
    public bool? AsSuggest
    {
        get => (Flags & StructFieldFlags.AsSuggest) != 0 ? true : null;
        set
        {
            if (value == true)
            {
                Flags |= StructFieldFlags.AsSuggest;
            }
            else
            {
                Flags &= ~StructFieldFlags.AsSuggest;
            }
        }
    }

    /// <summary>
    /// When calculating the up limit, use the original value.
    /// </summary>
    [NotMapped]
    public bool? UseOriginForUpLimit
    {
        get => (Flags & StructFieldFlags.UseOriginForUpLimit) != 0 ? true : null;
        set
        {
            if (value == true)
            {
                Flags |= StructFieldFlags.UseOriginForUpLimit;
            }
            else
            {
                Flags &= ~StructFieldFlags.UseOriginForUpLimit;
            }
        }
    }
    
    #endregion
    
    #region Enum
    
    /// <summary>
    /// The enum cascade limit.
    /// </summary>
    public int? Cascade { get; set; }

    /// <summary>
    /// Allow use enum value in any level.
    /// </summary>
    [NotMapped]
    public bool? AnyLevel
    {
        get => (Flags & StructFieldFlags.AnyLevel) != 0 ? true : null;
        set
        {
            if (value == true)
            {
                Flags |= StructFieldFlags.AnyLevel;
            }
            else
            {
                Flags &= ~StructFieldFlags.AnyLevel;
            }
        }
    }

    /// <summary>
    /// Don't allow flags enum value combination.
    /// </summary>
    [NotMapped]
    public bool? SingleFlag
    {
        get => (Flags & StructFieldFlags.SingleFlag) != 0 ? true : null;
        set
        {
            if (value == true)
            {
                Flags |= StructFieldFlags.SingleFlag;
            }
            else
            {
                Flags &= ~StructFieldFlags.SingleFlag;
            }
        }
    }

    #endregion
        
    #region Ref
    
    /// <summary>
    /// The type node ref
    /// </summary>
    [JsonIgnore]
    [NotMapped]
    public AnySchemaType? SchemeType { get; set; }

    #endregion

    #region Inner Type

    [Flags]
    public enum StructFieldFlags
    {
        None = 0,
        Require = 1 << 0,
        Immutable = 1 << 1,
        Readonly = 1 << 2,
        Invisible = 1 << 3,
        DisplayOnly = 1 << 4,
        AsSuggest = 1 << 5,
        UseOriginForUpLimit = 1 << 6,
        AnyLevel = 1 << 7,
        SingleFlag = 1 << 8,
        Unpack = 1 << 9,
    }

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
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// The relation function
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    [Schema(NS_SYSTEM_SCHEMA_FUNC_TYPE)]
    public string Func { get; set; } = string.Empty;

    /// <summary>
    /// The func arguments
    /// </summary>
    public FuncCallArg[] Args { get; set; } = [];

    /// <summary>
    /// The relationType type
    /// </summary>
    public RelationType Type { get; set; } = RelationType.Default;
    
    /// <summary>
    /// The schema node status
    /// </summary>
    [NotMapped]
    public SchemaNodeStatus? Status { get; set; }

    /// <summary>
    /// The function node ref
    /// </summary>
    [JsonIgnore]
    [NotMapped]
    public FunctionType? FuncNode { get; set; }
}
