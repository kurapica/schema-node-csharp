using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Common;

/// <summary>
/// THe disable property
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_STRUCT_FIELD, SCHEMA_KIND_ENUM_VALUE)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_COMMON}.{nameof(Disable)}")]
public class Disable: Property<bool>;