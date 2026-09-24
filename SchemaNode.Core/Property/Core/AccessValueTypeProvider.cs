using SchemaNode.Attribute;
using SchemaNode.Property.Common;
using SchemaNode.Property.Property;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Core;

/// <summary>
/// The access value type provider
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_STRING)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_CORE}.{nameof(AccessValueTypeProvider)}")]
[Meta<Static>(true)]
[Meta<ReadOnly>(true)]
[Meta<InVisible>(true)]
[Meta<PropertyValueType>($"{NS_SYSTEM_SCHEMA_FUNC_CALL}<{NS_SYSTEM_SCHEMA_FUNC}.valuetype>")]
public class AccessValueTypeProvider: FuncCallProperty;