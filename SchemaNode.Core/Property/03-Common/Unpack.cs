using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;
using Object = SchemaNode.Scalar.Object;

namespace SchemaNode.Property.Common;

/// <summary>
/// Declare the field with object type used as unpack field
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_STRUCT_FIELD)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<ForType>(typeof(Object))]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY}.{nameof(Unpack)}")]
public class Unpack : Property<bool>;