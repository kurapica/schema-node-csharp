using SchemaNode.Attribute;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Property;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Function;

/// <summary>
/// Marks a function as a type converter
/// </summary>
[Meta<Static>(true)]
[Meta<ReadOnly>(true)]
[Meta<ForSchema>(SCHEMA_KIND_NODE_FUNCTION)]
[Meta<OfNodeKind>(NODE_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_FUNC}.{nameof(Converter)}")]
public sealed class Converter : Property<bool>;
