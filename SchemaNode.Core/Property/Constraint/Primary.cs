using SchemaNode.Attribute;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Relation;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Constraint;

/// <summary>
/// The array primaries
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_ARRAY)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CONSTRAINT}.{nameof(Primary)}")]
[Meta<Static>(true)]
[Relation<Visible, Call>(NS_SYSTEM_SCHEMA_REFLECT_IS_SCHEMA_KIND, $"@{nameof(ArraySchema.Element)}", SCHEMA_KIND_STRUCT)]
[Relation<EntrySource, Assign>($"{nameof(Primary)}.{ARRAY_ELEMENT}", $"{NS_SYSTEM_SCHEMA_REFLECT_STRUCT}.{nameof(SchemaNode.Function.Reflect.Struct.getindexablefields)}", $"@{nameof(ArraySchema.Element)}")]
public class Primary : Property<string[]>, IConstraintProperty;