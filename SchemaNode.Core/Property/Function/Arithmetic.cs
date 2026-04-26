using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Property.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Function;

/// <summary>
/// Declares the arithmetic operation type of a function (used by expression compilers)
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_FUNCTION)]
public sealed class Arithmetic : Property<ArithmeticType>;
