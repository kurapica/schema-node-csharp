using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using SchemaNode.Property.Property;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Common;

/// <summary>
/// The data is immutable, means it can't be changed after it has original value
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_COMMON}.{nameof(Immutable)}")]
[Meta<Static>(true)]
public class Immutable : Property<bool>;