using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Constraint;

/// <summary>
/// The data indexes
/// </summary>
[Meta<Static>]
[Meta<ForSchema>(SCHEMA_KIND_ARRAY)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY}.{nameof(Indexes)}")]
public class Indexes : Property<DataIndex[]>, IConstraintProperty;


[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_ARRAY}.index")]
public sealed record DataIndex([Meta<UplimitString>(PRIMARY_KEY_MAX_LEN)] string Name, string[] Fields, bool IsUnique = false);