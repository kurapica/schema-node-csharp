using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using SchemaNode.Struct;
using static SchemaNode.Utility.Constant;


namespace SchemaNode.Property.Common;

/// <summary>
/// The unit label
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_STRUCT_FIELD, SCHEMA_KIND_INT, SCHEMA_KIND_DECIMAL, SCHEMA_KIND_STRING)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_COMMON}.{nameof(Unit)}")]
public class Unit : Property<LocaleString>;