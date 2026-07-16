using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Common;

/// <summary>
/// Declare the field with object type used as unpack field
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_STRUCT_FIELD)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_COMMON}.{nameof(Unpack)}")]
[Relation<Visible, Relation.Call>(NODE_SELF, NS_SYSTEM_SCHEMA_REFLECT_IS_VALUE_KIND, $"${nameof(StructFieldSchema.Type)}", SCHEMA_KIND_OBJECT)]
public class Unpack : Property<bool>;