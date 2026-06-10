using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;

namespace SchemaNode.Property.App;

/// <summary>
/// The application target policy
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_APP)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP}.{nameof(ScopePolicy)}")]
public class ScopePolicy : Property<AppScopePolicy>;
