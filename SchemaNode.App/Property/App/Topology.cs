using SchemaNode.Attribute;
using SchemaNode.Function;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;
using SchemaNode.Schema;

namespace SchemaNode.Property.App;

/// <summary>
/// The app field storage topology
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_APP_FIELD)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_APP}.{nameof(Topology)}")]
[Relation<InVisible, Relation.Call>(nameof(Topology), $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.not)}", $"@{nameof(EnableStorage)}")]
[Relation<Visible, Relation.Call>(nameof(Topology), $"{NS_SYSTEM_SCHEMA_REFLECT_STRUCT}.{nameof(SchemaNode.Function.Reflect.Struct.hasdynamicfield)}", $"@{nameof(AppFieldSchema.Type)}")]
public class Topology : Property<Enum.FieldStorageTopology>;