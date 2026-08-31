using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Array;

/// <summary>
/// The array primaries
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_ARRAY, SCHEMA_KIND_ARRAY_DEFINE)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_ARRAY}.{nameof(Primary)}")]
[Meta<Static>(true)]
public class Primary : Property<string[]>, IConstraintProperty;