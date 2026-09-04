using SchemaNode.Attribute;
using SchemaNode.Function;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Property;
using SchemaNode.Relation;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Array;

/// <summary>
/// The array primaries
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_ARRAY, SCHEMA_KIND_ARRAY_DEFINE)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_ARRAY}.{nameof(Primary)}")]
[Meta<Static>(true)]
[Relation<BlackList, Call>($"{nameof(Primary)}.{ARRAY_ELEMENT}", $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(Primary)}.{ARRAY_PREVIOUS}")]
public class Primary : Property<string[]>, IConstraintProperty;