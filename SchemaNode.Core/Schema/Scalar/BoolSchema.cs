using SchemaNode.Attribute;
using SchemaNode.Property.Bool;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Record;
using static SchemaNode.Utility.Constant;
using ValueSchemaKind = SchemaNode.Property.Record.ValueSchemaKind;

namespace SchemaNode.Schema;

/// <summary>
/// The bool schema kind is used to define a boolean value. It can be used to represent true/false values in the schema.
/// There is no meta data associated with the bool schema kind, as it is a simple scalar type. The bool schema kind is used in various contexts where a boolean value is required, such as in conditional statements or to represent binary states.
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_BOOL, SCHEMA_KIND_ORDER_BOOL)]
[Meta<NodeSchemaKind>(SCHEMA_KIND_BOOL, SCHEMA_KIND_ORDER_BOOL)]
[Meta<ValueSchemaKind>(SCHEMA_KIND_BOOL, SCHEMA_KIND_ORDER_BOOL)]
[Meta<NodeType>(typeof(Runtime.BoolType))]
[Meta<SchemaUsage>(typeof(BoolUsage))]
[Meta<BoolValue>]
[Meta<Append>(typeof(Default))]
public sealed class BoolKind;

/// <summary>
/// The bool usage
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_BOOL_USAGE, SCHEMA_KIND_ORDER_BOOL)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_BOOL}.usage")]
[Meta<Attach>(SCHEMA_KIND_BOOL_USAGE)]
[Meta<Append>(typeof(Default))]
public sealed class BoolUsage;

/// <summary>
/// Represents the bool scalar type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_BOOL}.type")]
[Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_IS_SCHEMA_KIND, NODE_SELF, SCHEMA_KIND_BOOL)]
public class BoolType : ValueType;