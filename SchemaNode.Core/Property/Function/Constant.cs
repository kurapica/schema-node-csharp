using SchemaNode.Attribute;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Function;

/// <summary>
/// Declares a function as a compile-time constant with the given value (used by expression compilers)
/// </summary>
[Meta<Default>(true)]
[Meta<ForSchema>(SCHEMA_KIND_FUNCTION)]
public sealed class Constant : Property<object>;
