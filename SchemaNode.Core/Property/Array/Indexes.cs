using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using SchemaNode.Property.Property;
using SchemaNode.Scalar;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Array;

/// <summary>
/// The data indexes
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_ARRAY, SCHEMA_KIND_ARRAY_DEFINE)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_ARRAY}.{nameof(Indexes)}")]
[Meta<Static>(true)]
public class Indexes : Property<DataIndex[]>, IConstraintProperty;


[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_ARRAY}.index")]
public sealed record DataIndex([Meta<SchemaType>(typeof(Identifier))] string Name, string[] Fields, bool IsUnique = false);