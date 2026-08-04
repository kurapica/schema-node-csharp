using SchemaNode.Attribute;
using SchemaNode.Property.Common;
using SchemaNode.Property.Constraint;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Core;

/// <summary>
/// The entry source consumer of entry source property
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_STRING)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CORE}.{nameof(EntrySourceConsumer)}")]
[Meta<Static>(true)]
[Meta<ReadOnly>(true)]
[Meta<InVisible>(true)]
public class EntrySourceConsumer: Property<Boolean>;