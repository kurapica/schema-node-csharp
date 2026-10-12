using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Property;

/// <summary>
/// The property is not inheritable, it can't be used in property chain access
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_NODE_PROPERTY)]
[Meta<OfNodeKind>(NODE_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_PROPERTY}.{nameof(NoInherit)}")]
[Meta<Static>(true)]
public class NoInherit : Property<bool>;
