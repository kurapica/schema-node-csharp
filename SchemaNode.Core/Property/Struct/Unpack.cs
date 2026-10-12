using SchemaNode.Attribute;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Property;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Struct;

/// <summary>
/// Declare the field with object type used as unpack field
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_NODE_STRUCT_FIELD)]
[Meta<OfNodeKind>(NODE_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_STRUCT}.{nameof(Unpack)}")]
[Relation<Visible, Relation.Call>(nameof(Unpack), NS_SYSTEM_SCHEMA_REFLECT_IS_NODE_KIND, $"@{nameof(StructFieldSchema.Type)}", false, NODE_KIND_OBJECT, NODE_KIND_STRUCT)]
public class Unpack : Property<bool>;