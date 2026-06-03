using System.ComponentModel.DataAnnotations.Schema;
using SchemaNode.Attribute;
using SchemaNode.Function;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;

namespace SchemaNode.Property.App;

/// <summary>
/// The app field as view for source app field
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_APP_FIELD)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP_FIELD}.{nameof(View)}")]
public class View : Property<View>;

[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP_FIELD}.view")]
public sealed class FieldView
{
    /// <summary>
    /// The source application
    /// </summary>
    [Meta<SchemaType>(typeof(AppType))]
    public string App { get; set; } = string.Empty;

    /// <summary>
    /// The source field
    /// </summary>
    [Meta<EntrySource>($"{NS_SYSTEM_SCHEMA_REFLECT}.{nameof(SystemAppReflect.getappfields)}", $"${nameof(App)}")]
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// The target map field
    /// </summary>
    public string Map { get; set; } = string.Empty;

    [NotMapped]
    public AppType? AppType { get; set; }
}
