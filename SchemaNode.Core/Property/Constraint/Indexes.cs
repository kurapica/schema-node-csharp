using SchemaNode.Attribute;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Relation;
using SchemaNode.Scalar;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Constraint;

/// <summary>
/// The data indexes
/// </summary>
[Meta<Static>(true)]
[Meta<ForSchema>(SCHEMA_KIND_ARRAY)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CONSTRAINT}.{nameof(Indexes)}")]
[Relation<Visible, Call>(nameof(Indexes), NS_SYSTEM_SCHEMA_REFLECT_IS_SCHEMA_KIND, $"@{nameof(ArraySchema.Element)}", SCHEMA_KIND_STRUCT)]
[Relation<EntrySource, Assign>($"{nameof(Indexes)}.{nameof(DataIndex.Fields)}.{ARRAY_ELEMENT}", $"{NS_SYSTEM_SCHEMA_REFLECT_STRUCT}.{nameof(SchemaNode.Function.Reflect.Struct.getindexablefields)}", $"@{nameof(ArraySchema.Element)}")]
public class Indexes : Property<DataIndex[]>, IConstraintProperty;


[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_ARRAY}.index")]
public sealed record DataIndex([Meta<SchemaType>(typeof(Identifier))] string Name, string[] Fields, bool IsUnique = false);