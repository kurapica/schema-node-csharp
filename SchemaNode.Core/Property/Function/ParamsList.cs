using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using SchemaNode.Property.Property;
using SchemaNode.Struct;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Function;

/// <summary>
/// The param list for variadic argument
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_FUNC_ARG)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_FUNC}.{nameof(ParamsList)}")]
[Meta<PropertyValueType>($"{NS_SYSTEM_LIST}<{NS_SYSTEM_ENTRY}<{NS_SYSTEM_SCHEMA_NODE}.valuetype>>")]
public class ParamsList: Property<Entry<string>>;