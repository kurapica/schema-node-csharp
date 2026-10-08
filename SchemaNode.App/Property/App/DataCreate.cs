using SchemaNode.Attribute;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Property;
using SchemaNode.Property.Struct;
using static SchemaNode.Utility.AppConstant;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.App;

/// <summary>
/// Allow data create
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_APP_FIELD)]
[Meta<OfNodeKind>(NODE_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_APP}.{nameof(DataCreate)}")]
[Meta<Static>(true)]
[Meta<InVisible>(true)]
[Meta<DisplayOnly>(true)]
public class DataCreate: Property<bool>;