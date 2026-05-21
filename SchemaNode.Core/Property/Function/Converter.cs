using SchemaNode.Attribute;
using SchemaNode.Property.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Function;

/// <summary>
/// Marks a function as a type converter
/// </summary>
[Meta<Default>(true)]
[Meta<ForSchema>(SCHEMA_KIND_FUNCTION)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY}.{nameof(Converter)}")]
public sealed class Converter : Property<bool>;
