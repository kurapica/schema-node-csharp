using SchemaNode.Attribute;
using System.ComponentModel.DataAnnotations;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Struct;

/// <summary>
/// The dict entry
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_ENTRY)]
public sealed class Entry
{
    /// <summary>
    /// The entry value
    /// </summary>
    [Meta<UplimitStringProperty>(PRIMARY_KEY_MAX_LEN)]
    [Meta<UniqueIndex>]
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
