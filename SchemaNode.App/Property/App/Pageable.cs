using SchemaNode.Attribute;
using SchemaNode.Function;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;

namespace SchemaNode.Property.App;

/// <summary>
/// The app field is using increase update mode, no full data push allowed, always using page query
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_APP_FIELD)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_APP}.{nameof(Pageable)}")]
[Relation<InVisible, Relation.Call>(NODE_SELF, $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.not)}", $"@{nameof(EnableStorage)}")]
[Relation<Visible, Relation.Call>(NODE_SELF, $"{NS_SYSTEM_SCHEMA_REFLECT}.{nameof(SystemReflect.isschemakind)}", $"@{nameof(Type)}", SCHEMA_KIND_ARRAY)]
public class Pageable : Property<bool>;