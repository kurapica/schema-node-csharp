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
/// The app field foreign settings
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_APP_FIELD)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_APP}.{nameof(Foreigns)}")]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Relation<Visible, Relation.Call>(NODE_SELF, $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(EnableStorage)}")]
public class Foreigns : Property<Foreign[]>;

/// <summary>
/// The foreign settings
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP_FIELD}.foreign")]
public sealed class Foreign
{
    /// <summary>
    /// The foreign app name
    /// </summary>
    [Meta<SchemaType>(typeof(AppType))]
    [Meta<PrimaryIndex>]
    public string App { get; set; } = string.Empty;
    
    /// <summary>
    /// The field refer to the other app target
    /// </summary>
    [Meta<EntrySource>($"{NS_SYSTEM_SCHEMA_REFLECT_APP}.{nameof(SystemAppReflect.getappfields)}", $"@{nameof(App)}")]
    [Meta<SchemaType>(typeof(Identifier))]
    public string Field { get; set; } = string.Empty;
    
    [JsonIgnore]
    [SchemaIgnore]
    public Runtime.AppType? AppType { get; set; }
}
