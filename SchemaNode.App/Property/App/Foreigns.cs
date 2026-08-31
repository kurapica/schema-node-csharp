using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Function;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Enum;
using SchemaNode.Property.String;
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
[Meta<Static>(true)]
[Relation<Visible, Relation.Call>(nameof(Foreigns), $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(EnableStorage)}")]
[Relation<BlackList, Relation.Call>($"{nameof(Foreigns)}.{nameof(Foreign.App)}", $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.newarray)}", $"@{nameof(AppFieldSchema.App)}")]
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
    [Meta<AccessEntryConsumer>(NS_SYSTEM_SCHEMA_REFLECT_IS_SCHEMA_KIND, NODE_SELF, false, SCHEMA_KIND_STRING)]
    [Meta<Cascade>(1)]
    [Meta<SchemaType>(typeof(Identifier))]
    public string Field { get; set; } = string.Empty;
    
    [JsonIgnore]
    [SchemaIgnore]
    public Runtime.AppType? AppType { get; set; }
}
