using SchemaNode.Attribute;
using SchemaNode.Function;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;

namespace SchemaNode.Property.App;

/// <summary>
/// The app field storage topology
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_APP_FIELD)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP_FIELD}.{nameof(Topology)}")]
[Relation<Visible>($"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"${nameof(EnableStorage)}")]
public class Topology : Property<Enum.FieldStorageTopology>;