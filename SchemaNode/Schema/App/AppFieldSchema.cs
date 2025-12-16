using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Runtime;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Schema;

/// <summary>
/// The application field schema
/// </summary>
[SchemaApp]
public class AppFieldSchema
{
    /// <summary>
    /// the application name
    /// </summary>
    [Index]
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string App { get; set; } = string.Empty;

    /// <summary>
    /// The field name
    /// </summary>
    [Index]
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string Name { get; set; } = default!;
    
    /// <summary>
    /// The seqno
    /// </summary>
    public int Seqno { get; set; }

    /// <summary>
    /// The field type
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string Type { get; set; } = default!;
    
    /// <summary>
    /// The field display name
    /// </summary>
    public LocaleString? Display { get; set; }
    
    /// <summary>
    /// The field description
    /// </summary>
    public LocaleString? Desc { get; set; }
    
    /// <summary>
    /// The source application
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string? SourceApp { get; set; }
    
    /// <summary>
    /// The source field
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string? SourceField { get; set; }
    
    /// <summary>
    /// The calculate function
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string? Func { get; set; }
    
    /// <summary>
    /// The input fields
    /// </summary>
    public string[]? Args { get; set; }

    /// <summary>
    /// The authentication policy
    /// </summary>
    public PolicyItem[]? Auths { get; set; }

    /// <summary>
    /// Row filter policy
    /// </summary>
    public RowPolicyItem[]? RowAuths { get; set; }

    /// <summary>
    /// The column access policy
    /// </summary>
    public ColPolicyItem[]? ColAuths { get; set; }
    
    /// <summary>
    /// The field flags
    /// </summary>
    public AppFieldFlags Flags { get; set; } = AppFieldFlags.None;

    /// <summary>
    /// The field is front-end only, no data storage
    /// </summary>
    [NotMapped]
    public bool? Frontend
    {
        get => (Flags & AppFieldFlags.Frontend) != 0 ? true : null;
        set
        {
            if (value == true)
            {
                Flags |= AppFieldFlags.Frontend;
            }
            else
            {
                Flags &= ~AppFieldFlags.Frontend;
            }
        }
    }

    /// <summary>
    /// The field is disabled
    /// </summary>
    [NotMapped]
    public bool? Disable
    {
        get => (Flags & AppFieldFlags.Disable) != 0 ? true : null;
        set
        {
            if (value == true)
            {
                Flags |= AppFieldFlags.Disable;
            }
            else
            {
                Flags &= ~AppFieldFlags.Disable;
            }
        }
    }

    /// <summary>
    /// The field is readonly, data comes from other apps
    /// </summary>
    [NotMapped]
    public bool? Readonly
    {
        get => (Flags & AppFieldFlags.Readonly) != 0 ? true : null;
        set
        {
            if (value == true)
            {
                Flags |= AppFieldFlags.Readonly;
            }
            else
            {
                Flags &= ~AppFieldFlags.Readonly;
            }
        }
    }

    /// <summary>
    /// The field is using increase update, no full data push allowed
    /// </summary>
    [NotMapped]
    public bool? IncrUpdate
    {
        get => (Flags & AppFieldFlags.IncrUpdate) != 0 ? true : null;
        set
        {
            if (value == true)
            {
                Flags |= AppFieldFlags.IncrUpdate;
            }
            else
            {
                Flags &= ~AppFieldFlags.IncrUpdate;
            }
        }
    }

    /// <summary>
    /// Track the push data to the source field, so toggle the source target, will also re-push the data
    /// </summary>
    [NotMapped]
    public bool? TrackPush
    {
        get => (Flags & AppFieldFlags.TrackPush) != 0 ? true : null;
        set
        {
            if (value == true)
            {
                Flags |= AppFieldFlags.TrackPush;
            }
            else
            {
                Flags &= ~AppFieldFlags.TrackPush;
            }
        }
    }

    /// <summary>
    /// The combine rule for scalar/enum type
    /// </summary>
    public DataCombineType? Combine { get; set; }
    
    /// <summary>
    /// The combine rule for struct or struct-array type
    /// </summary>
    public DataCombine[]? Combines { get; set; }

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
    
    #region Inner Type

    [Flags]
    public enum AppFieldFlags
    {
        None = 0,
        Frontend = 1 << 0,
        Disable = 1 << 1,
        Readonly = 1 << 2,
        IncrUpdate = 1 << 3,
        TrackPush = 1 << 4,
    }

    #endregion
}

/// <summary>
/// The row policy item
/// </summary>
public class RowPolicyItem
{
    /// <summary>
    /// The policy evaluatorm, if true will use the filter
    /// </summary>
    [Schema(NS_SYSTEM_SCHEMA_EVALUATOR_FUNC_TYPE)]
    public required string Evaluator { get; set; }

    /// <summary>
    /// The row filter function
    /// </summary>
    [Schema(NS_SYSTEM_SCHEMA_PREDICATE_FUNC_TYPE)]
    public string? Filter { get; set; }

    /// <summary>
    /// The function type of the evaluator
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    public FunctionType? EvaluatorFunc { get; set; }

    /// <summary>
    /// The function type of the filter
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    public FunctionType? FilterFunc { get; set; }
}

/// <summary>
/// The column policy item
/// </summary>
public class ColPolicyItem
{
    /// <summary>
    /// The struct field name
    /// </summary>
    public required string Name { get; set; } = string.Empty;

    /// <summary>
    /// The column access evaluators
    /// </summary>
    public string[] Evaluators { get; set; } = [];

    /// <summary>
    /// The function type of the evaluator
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    public FunctionType[] Functions { get; set; } = [];
}