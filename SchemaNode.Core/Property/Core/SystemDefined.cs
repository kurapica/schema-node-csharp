using SchemaNode.Attribute;
using SchemaNode.Property.Common;
using SchemaNode.Property.Property;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Core;

[Meta<Alias>("system")]
[Meta<ForSchema>(SCHEMA_KIND_NODE)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<Static>(true)]
[Meta<ReadOnly>(true)]
[Meta<InVisible>(true)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_CORE}.{nameof(SystemDefined)}")]
public class SystemDefined: Property<bool>;