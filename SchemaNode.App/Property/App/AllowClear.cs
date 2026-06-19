using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;

namespace SchemaNode.Property.App;

/// <summary>
/// All clear the app field data
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_APP_FIELD)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP_FIELD}.{nameof(AllowClear)}")]
public class AllowClear : Property<bool>;