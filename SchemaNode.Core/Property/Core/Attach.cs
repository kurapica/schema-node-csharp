using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Property.Common;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Core;

/// <summary>
/// Attach the properties of the given schema kind
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_STRUCT)]
[Meta<ReadOnly>(true)]
[Meta<Static>]
[Meta<PropertyValueType>(typeof(SchemaKind))]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CORE}.{nameof(Attach)}")]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
public class Attach : Property<string>;