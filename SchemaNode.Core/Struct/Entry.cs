using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Struct;

/// <summary>
/// The dict entry
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_ENTRY)]
public sealed class Entry<T>
{
    /// <summary>
    /// The entry value
    /// </summary>
    [Meta<PrimaryIndex>]
    public T Value { get; set; } = default!;

    /// <summary>
    /// The entry label
    /// </summary>
    public LocaleString? Label { get; set; }
    
    /// <summary>
    /// Has children
    /// </summary>
    public bool? HasChildren { get; set; }
}