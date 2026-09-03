using SchemaNode.Attribute;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Property;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Struct;

/// <summary>
/// The override type
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_STRUCT_FIELD)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<Visible>(false)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_STRUCT}.{nameof(OverrideFields)}")]
[Meta<PropertyValueType>(typeof(Schema.ValueType))]
public class OverrideFields : Property<StructFieldSchema[]>;