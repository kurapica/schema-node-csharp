using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Property;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Schema;
using SchemaNode.Struct;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Schema;

/// <summary>
/// The enum schema
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_ENUM}.schema")]
[Meta<AsSchemaKind>(nameof(EnumSchema), SCHEMA_KIND_ORDER_ENUM)]
[Meta<AsNodeSchemaKind>(nameof(EnumSchema), SCHEMA_KIND_ORDER_ENUM)]
public sealed class EnumSchema : ExtensibleSchema
{
    /// <summary>
    /// The enum value type
    /// </summary>
    public EnumValueType Type { get; set; }
    
    /// <summary>
    /// The cascades of the enum value
    /// </summary>
    public LocaleString[]? Cascade { get; set; }

    /// <summary>
    /// The enum values
    /// </summary>
    public EnumValueInfo[] Values { get; set; } = [];
}

/// <summary>
/// Declare enum property for node schema
/// </summary>
[Meta<ForSchema>(nameof(NodeSchema))]
public sealed class EnumProperty: Property<EnumSchema>;

/// <summary>
/// The enum value info
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_ENUM}.value")]
public sealed class EnumValueInfo
{
    /// <summary>
    /// The value
    /// </summary>
    [Meta<UniqueIndex>]
    [Meta<UniqueIndex>("SUB_LIST", 1)]
    [Meta<UplimitStringProperty>(PRIMARY_KEY_MAX_LEN)]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// The root value
    /// </summary>
    [Meta<UniqueIndex>("SUB_LIST", 0)]
    [Meta<UplimitStringProperty>(PRIMARY_KEY_MAX_LEN)]
    public string? Root { get; set; }

    /// <summary>
    /// The name of the enum value
    /// </summary>
    public LocaleString Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether the enum value is disabled
    /// </summary>
    public bool? Disable  { get; set; }
    
    #region Runtime info
    
    /// <summary>
    /// Whether the enum value has sub enum values
    /// </summary>
    [NotMapped]
    public bool? HasSubList { get; set; }
    
    /// <summary>
    /// The sub enum values
    /// </summary>
    [NotMapped]
    public EnumValueInfo[]? SubList { get; set; }

    /// <summary>
    /// Whether the enum value is fully loaded
    /// </summary>
    [JsonIgnore]
    [NotMapped]
    internal bool IsFullyLoaded { get; set; }

    /// <summary>
    /// The parent of the enum value
    /// </summary>
    [JsonIgnore]
    [NotMapped]
    internal EnumValueInfo? Parent { get; set; }

    /// <summary>
    /// The cascade level
    /// </summary>
    [JsonIgnore]
    [NotMapped]
    internal int Level { get; set;  }
    
    #endregion
    
    /// <summary>
    /// Clones the enum value with limit level
    /// </summary>
    /// <param name="limitLevel"></param>
    /// <returns></returns>
    internal EnumValueInfo Clone(int limitLevel = 0)
    {
        return new EnumValueInfo
        {
            Value = Value,
            Name = Name,
            Disable = Disable,
            HasSubList = HasSubList,
            SubList = (HasSubList ?? false) && SubList is { Length: > 0 } && limitLevel > 0 
                ? SubList.Select(e => e.Clone(limitLevel - 1)).ToArray()
                : null,
        };
    }
}

/// <summary>
/// The enum value access info
/// </summary>
public sealed class EnumValueAccess
{
    /// <summary>
    /// The cascade name
    /// </summary>
    public LocaleString? Name { get; set; }
    
    /// <summary>
    /// The enum value of the cascade
    /// </summary>
    public string Value { get; set; } = string.Empty;
    
    /// <summary>
    /// The sublist of the enum value
    /// </summary>
    public EnumValueInfo[]? SubList { get; set; }
}