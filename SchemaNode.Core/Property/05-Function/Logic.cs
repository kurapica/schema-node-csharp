using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Function;

/// <summary>
/// Declares the logic operation type of a function (used by expression compilers)
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_FUNCTION)]
public sealed class Logic : Property<LogicType>;
