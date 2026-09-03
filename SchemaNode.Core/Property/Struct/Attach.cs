using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Property;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Struct;

/// <summary>
/// Attach the properties of the given schema kind
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_STRUCT)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<ReadOnly>(true)]
[Meta<Static>(true)]
[Meta<InVisible>(true)]
[Meta<PropertyValueType>(typeof(SchemaKind))]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_STRUCT}.{nameof(Attach)}")]
public class Attach : Property<string>;