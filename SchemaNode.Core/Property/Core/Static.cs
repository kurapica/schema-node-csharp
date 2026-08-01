using SchemaNode.Attribute;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Core;

/// <summary>
/// The property is static, can't be changed by relations
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CORE}.{nameof(Static)}")]
[Meta<Static>(true)]
public class Static : Property<bool>;
