using SchemaNode.Attribute;
using SchemaNode.Property.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Constraint;

/// <summary>
/// The array primaries
/// </summary>
[Meta<Static>]
[Meta<ForSchema>(SCHEMA_KIND_ARRAY)]
public class Primary : Property<string[]>, IConstraintProperty;