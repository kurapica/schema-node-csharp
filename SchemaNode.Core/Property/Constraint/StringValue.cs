using SchemaNode.Attribute;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Constraint;

/// <summary>
/// The string value constraint
/// </summary>
[Meta<Alias>("string")]
[Meta<ForSchema>(SCHEMA_KIND_STRUCT)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CONSTRAINT}.string")]
[Meta<Default>(true)]
[Meta<InVisible>(true)] // root only
[Meta<Static>(true)]
public class StringValue: Property<bool>, IConstraintProperty;