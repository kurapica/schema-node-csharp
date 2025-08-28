using SchemaNode.Enum;

namespace SchemaNode.Schema;

/// <summary>
/// The enum type schema
/// </summary>
public class EnumSchema
{
    /// <summary>
    /// The enum value type
    /// </summary>
    public EnumValueType Type { get; set; }

    /// <summary>
    /// The cascades of the enum value
    /// </summary>
    public string[]? Cascade { get; set; }

    /// <summary>
    /// The enum values
    /// </summary>
    public EnumValueInfo[] Values { get; set; } = [];
}

/// <summary>
/// The enum value info
/// </summary>
public class EnumValueInfo
{
    /// <summary>
    /// The value
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// The name of the enum value
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether the enum value is disabled
    /// </summary>
    public bool? Disable  { get; set; }

    /// <summary>
    /// Whether the enum value has sub enum values
    /// </summary>
    public bool? HasSubList { get; set; }

    /// <summary>
    /// The sub enum values
    /// </summary>
    public EnumValueInfo[]? SubList { get; set; }
}