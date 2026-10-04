using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Function;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Property;
using static SchemaNode.Utility.AppConstant;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.App;

/// <summary>
/// The storage table name
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_APP_FIELD)]
[Meta<OfNodeKind>(NODE_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_APP}.{nameof(AttrTableName)}")]
[Relation<InVisible, Relation.Call>(nameof(AttrTableName), $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.not)}", $"@{nameof(EnableStorage)}")]
[Relation<Visible, Relation.Call>(nameof(AttrTableName), NS_SYSTEM_LOGIC_EQ, $"@{nameof(Topology)}", FieldStorageTopology.AttributeBased)]
public class AttrTableName : Property<string>;