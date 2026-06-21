using SchemaNode.Attribute;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Function;

/// <summary>
/// Marks a function as a type converter
/// </summary>
[Meta<Static>]
[Meta<ReadOnly>(true)]
[Meta<ForSchema>(SCHEMA_KIND_FUNCTION)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_FUNC}.{nameof(Converter)}")]
public sealed class Converter : Property<bool>;
