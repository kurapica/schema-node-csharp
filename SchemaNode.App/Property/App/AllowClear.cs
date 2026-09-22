using SchemaNode.Attribute;
using SchemaNode.Function;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Property;
using static SchemaNode.Utility.AppConstant;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.App;

/// <summary>
/// All clear the app field data
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_APP_FIELD)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_APP}.{nameof(AllowClear)}")]
[Relation<InVisible, Relation.Call>(nameof(AllowClear), $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.not)}", $"@{nameof(EnableStorage)}")]
public class AllowClear : Property<bool>;