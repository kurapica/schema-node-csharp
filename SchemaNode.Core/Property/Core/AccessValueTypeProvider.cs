using SchemaNode.Attribute;
using SchemaNode.Property.Common;
using SchemaNode.Property.Constraint;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Core;

/// <summary>
/// The access value type provider
/// </summary>
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CORE}.{nameof(AccessValueTypeProvider)}")]
[Meta<Static>(true)]
[Meta<ReadOnly>(true)]
[Meta<InVisible>(true)]
[Relation<Valid, Relation.Assign>($"{nameof(AccessValueTypeProvider)}.{nameof(FuncCall.Func)}", NS_SYSTEM_SCHEMA_REFLECT_FUNC_WITH_RETURN, NODE_SELF, $"{NS_SYSTEM_SCHEMA_NODE}.valuetype")]
public class AccessValueTypeProvider: FuncCallProperty;