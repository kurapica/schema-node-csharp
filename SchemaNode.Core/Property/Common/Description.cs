using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using SchemaNode.Struct;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Common;

/// <summary>
/// The description property, which is used to specify the description information
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_NODE, SCHEMA_KIND_STRUCT_FIELD, SCHEMA_KIND_ENUM_VALUE)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_COMMON}.{nameof(Description)}")]
public class Description : Property<LocaleString>;