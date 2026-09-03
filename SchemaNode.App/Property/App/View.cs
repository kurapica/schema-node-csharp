using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Function;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Enum;
using SchemaNode.Property.Struct;
using SchemaNode.Property.Property;
using SchemaNode.Relation;
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
[Relation<InVisible, Call>(nameof(View), $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(EnableStorage)}")]
[Relation<Default, Call>($"{nameof(View)}.{nameof(FieldView.Owner)}", $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(App)}")]
public class View : Property<FieldView>;

/// <summary>
/// The view settings
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP_FIELD}.view")]
public sealed class FieldView
{
    /// <summary>
    /// The owner application
    /// </summary>
    [Meta<SchemaType>(typeof(AppType))]
    [Meta<DisplayOnly>(true)]
    [Meta<InVisible>(true)]
    public string Owner { get; set; } = string.Empty;

    /// <summary>
    /// The source application
    /// </summary>
    [Meta<SchemaType>(typeof(AppType))]
    public string App { get; set; } = string.Empty;

    /// <summary>
    /// The source field
    /// </summary>
    [Meta<EntrySource>($"{NS_SYSTEM_SCHEMA_REFLECT_APP}.{nameof(SystemReflectApp.getappforeignfields)}", $"@{nameof(App)}", $"@{nameof(Owner)}")]
    [Meta<SchemaType>(typeof(Identifier))]
    public string Field { get; set; } = string.Empty;
    
    /// <summary>
    /// The field value type
    /// </summary>
    [Meta<DisplayOnly>(true)]
    [Meta<InVisible>(true)]
    [Relation<Default, Call>(NODE_SELF, $"{NS_SYSTEM_SCHEMA_REFLECT_APP}.{nameof(SystemReflectApp.getappfieldtype)}", $"@{nameof(App)}", $"@{nameof(Field)}", true)]
    public string? FieldType { get; set; }

    /// <summary>
    /// The target map field
    /// </summary>
    [Meta<SchemaType>(typeof(Identifier))]
    [Meta<EntrySource>($"{NS_SYSTEM_SCHEMA_REFLECT_TYPE}.{nameof(SchemaNode.Function.Reflect.Type.gettypeentries)}", $"@{nameof(FieldType)}")]
    [Meta<Cascade>(1)]
    [Meta<Valid>($"{NS_SYSTEM_SCHEMA_REFLECT_TYPE}.{nameof(SchemaNode.Function.Reflect.Type.isschemakindaccess)}", $"@{nameof(FieldType)}", NODE_SELF, false, SCHEMA_KIND_STRING)]
    public string Map { get; set; } = string.Empty;

    [SchemaIgnore]
    [JsonIgnore]
    public Runtime.AppType? AppType { get; set; }
}