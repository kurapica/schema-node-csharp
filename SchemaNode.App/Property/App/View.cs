using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Function;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;
using SchemaNode.Scalar;

namespace SchemaNode.Property.App;

/// <summary>
/// The app field view settings
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_APP_FIELD)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_APP}.{nameof(View)}")]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<Static>(true)]
[Relation<InVisible, Relation.Call>(NODE_SELF, $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(EnableStorage)}")]
public class View : Property<FieldView>;

/// <summary>
/// The view settings
/// </summary>
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
    [Meta<SchemaType>(typeof(Identifier))]
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// The target map field
    /// </summary>
    [Meta<SchemaType>(typeof(Identifier))]
    public string Map { get; set; } = string.Empty;

    [SchemaIgnore]
    [JsonIgnore]
    public Runtime.AppType? AppType { get; set; }
}