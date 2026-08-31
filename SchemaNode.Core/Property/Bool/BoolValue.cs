using SchemaNode.Attribute;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Bool;

/// <summary>
/// The bool value constraint
/// </summary>
[Meta<Alias>("bool")]
[Meta<ForSchema>(SCHEMA_KIND_BOOL)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_BOOL}.valid")]
[Meta<Default>(true)]
[Meta<InVisible>(true)] // root only
[Meta<Static>(true)]
public class BoolValue: Property<bool>, IConstraintProperty;