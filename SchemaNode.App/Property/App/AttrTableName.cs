using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Function;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;

namespace SchemaNode.Property.App;

/// <summary>
/// The storage table name
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_APP_FIELD)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_APP}.{nameof(AttrTableName)}")]
[Relation<Visible, Relation.Call>(NODE_SELF, NS_SYSTEM_LOGIC_EQ, $"${nameof(Topology)}", FieldStorageTopology.AttributeBased)]
public class AttrTableName : Property<string>;