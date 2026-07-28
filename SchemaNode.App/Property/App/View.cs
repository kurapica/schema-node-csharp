using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Function;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;

namespace SchemaNode.Property.App;

[Meta<ForSchema>(SCHEMA_KIND_APP_FIELD)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_APP}.{nameof(View)}")]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Relation<Visible, Relation.Call>(NODE_SELF, $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(EnableStorage)}")]
public class View : Property<FieldView>;


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
    [Meta<EntrySource>($"{NS_SYSTEM_SCHEMA_REFLECT_APP}.{nameof(SystemAppReflect.getappfields)}", $"@{nameof(App)}")]
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// The target map field
    /// </summary>
    public string Map { get; set; } = string.Empty;

    [SchemaIgnore]
    [JsonIgnore]
    public Runtime.AppType? AppType { get; set; }
}