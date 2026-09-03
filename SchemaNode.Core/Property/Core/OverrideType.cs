using SchemaNode.Attribute;
using SchemaNode.Property.Common;
using SchemaNode.Property.Property;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Core;

/// <summary>
/// The override type
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<Visible>(false)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_CORE}.{nameof(OverrideType)}")]
[Meta<PropertyValueType>(typeof(Schema.ValueType))]
public class OverrideType : Property<string>;