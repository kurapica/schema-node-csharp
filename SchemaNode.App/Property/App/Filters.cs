using System.ComponentModel.DataAnnotations;
using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;

namespace SchemaNode.Property.App;

/// <summary>
/// The app field filters
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_APP_FIELD)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP_FIELD}.{nameof(Filters)}")]
public class Filters : Property<FieldFilter[]>;

/// <summary>
/// The field filter
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP_FIELD}.filter")]
public sealed class FieldFilter
{
    /// <summary>
    /// The filter mode
    /// </summary>
    public FieldFilterMode Mode { get; set; } = FieldFilterMode.Exactly;

    /// <summary>
    /// The field name or filter function
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string Filter { get; set; } = string.Empty;
    
    /// <summary>h
    /// The field filter resolve type, which defines how to resolve the filter when no contains found
    /// </summary>
    public FieldFilterResolve? Resolve { get; set; }
}