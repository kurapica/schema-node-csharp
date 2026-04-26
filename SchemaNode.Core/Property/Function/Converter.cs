using SchemaNode.Attribute;
using SchemaNode.Property.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Function;

/// <summary>
/// Marks a function as a type converter
/// </summary>
[Meta<Default>(true)]
[Meta<ForSchema>(SCHEMA_KIND_FUNCTION)]
public sealed class Converter : Property<bool>;
