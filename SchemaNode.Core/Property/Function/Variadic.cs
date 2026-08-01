using SchemaNode.Attribute;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Function;

/// <summary>
/// Marks a function argument as variadic
/// </summary>
[Meta<Static>(true)]
[Meta<ReadOnly>(true)]
[Meta<ForSchema>(SCHEMA_KIND_FUNC_ARG)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_FUNC}.{nameof(Variadic)}")]
public class Variadic : Property<bool>;