using SchemaNode.Attribute;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Property;
using SchemaNode.Property.Struct;
using static SchemaNode.Utility.AppConstant;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.App;

/// <summary>
/// Allow delete
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_NODE, SCHEMA_KIND_APP, SCHEMA_KIND_APP_FIELD, SCHEMA_KIND_APP_WORKFLOW)]
[Meta<OfNodeKind>(NODE_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_APP}.{nameof(SchemaDelete)}")]
[Meta<Static>(true)]
[Meta<InVisible>(true)]
[Meta<DisplayOnly>(true)]
public class SchemaDelete: Property<bool>;