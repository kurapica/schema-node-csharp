using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Property;

/// <summary>
/// The property is static, can't be changed by relations
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_PROPERTY}.{nameof(Stackable)}")]
[Meta<Static>(true)]
public sealed class Stackable : Property<bool>;
