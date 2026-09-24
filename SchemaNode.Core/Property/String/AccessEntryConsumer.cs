using SchemaNode.Attribute;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Property;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.String;

/// <summary>
/// The access value type consumer for ancestor provider
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_STRING)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_STRING}.{nameof(AccessEntryConsumer)}")]
[Meta<Static>(true)]
[Meta<ReadOnly>(true)]
[Meta<InVisible>(true)]
[Meta<PropertyValueType>($"{NS_SYSTEM_SCHEMA_FUNC_CALL}<{NS_SYSTEM_SCHEMA_FUNC}.valid>")]
public class AccessEntryConsumer: FuncCallProperty;