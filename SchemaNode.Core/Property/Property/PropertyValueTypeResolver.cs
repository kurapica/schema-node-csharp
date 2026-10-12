using SchemaNode.Attribute;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Property;

/// <summary>
/// The property value type resolver with owner
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_NODE_PROPERTY)]
[Meta<OfNodeKind>(NODE_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_PROPERTY}.{nameof(PropertyValueTypeResolver)}")]
[Meta<PropertyValueType>(typeof(FuncType))]
[Meta<Static>(true)]
[Meta<InVisible>(true)]
[Meta<ReadOnly>(true)]
public class PropertyValueTypeResolver : Property<string>;
