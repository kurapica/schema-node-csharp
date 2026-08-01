using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using SchemaNode.Scalar;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Constraint;

/// <summary>
/// The data indexes
/// </summary>
[Meta<Static>(true)]
[Meta<ForSchema>(SCHEMA_KIND_ARRAY)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CONSTRAINT}.{nameof(Indexes)}")]
public class Indexes : Property<DataIndex[]>, IConstraintProperty;


[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_ARRAY}.index")]
public sealed record DataIndex([Meta<SchemaType>(typeof(Identifier))] string Name, string[] Fields, bool IsUnique = false);