using SchemaNode.Attribute;
using SchemaNode.Property.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Constraint;

/// <summary>
/// The type property defines the expected schema type of the value, and is used for relationship
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_STRUCT_FIELD)]
public class Type : Property<string>, IConstraintProperty;
