using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using SchemaNode.Property.Property;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Common;

/// <summary>
/// The data init mechanism
/// </summary>
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_COMMON}.init")]
[Meta<PropertyValueType>($"{NS_SYSTEM_SCHEMA_FUNC_CALL}<{NS_SYSTEM_SCHEMA_FUNC}.type>")]
public class Init : FuncCallProperty;