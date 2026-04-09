using SchemaNode.Attribute;
using System.ComponentModel.DataAnnotations;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Struct;

/// <summary>
/// The dict entry
/// </summary>
[Schema(NS_SYSTEM_ENTRY)]
public sealed class Entry
{
    /// <summary>
    /// The entry value
    /// </summary>
    [Index]
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// The entry label
    /// </summary>
    public LocaleString? Label { get; set; }

    /// <summary>
    /// The entry children
    /// </summary>
    public Entry[]? Children { get; set; }
}
