using SchemaNode.Enum;
using SchemaNode.Attribute;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Components.Property.Constraint;

/// <summary>
/// The type property defines the expected schema type of the value, and is used for relationship
/// </summary>
[SchemaProperty([SchemaType.StructField, SchemaType.AppField], [ValueSchemaType.All], schemaType: NS_SYSTEM_SCHEMA_TYPE_RULE_VALUE)]
public class TypeProperty : SchemaProperty<string>, IConstraintProperty
{
}
