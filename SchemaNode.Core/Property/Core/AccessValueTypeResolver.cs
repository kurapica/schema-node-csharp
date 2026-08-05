using SchemaNode.Attribute;
using SchemaNode.Property.Common;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Core;

/// <summary>
/// The access value type resolver
/// </summary>
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CORE}.{nameof(AccessValueTypeResolver)}")]
[Meta<Static>(true)]
[Meta<ReadOnly>(true)]
[Meta<InVisible>(true)]
public class AccessValueTypeResolver: Property<string>;