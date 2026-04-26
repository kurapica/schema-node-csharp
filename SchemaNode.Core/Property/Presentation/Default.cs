using SchemaNode.Attribute;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Schema;

/// <summary>
/// The default value
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_SCALAR, SCHEMA_KIND_STRUCT_FIELD)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY}.{nameof(Default)}")]
public class Default: Property<object>;