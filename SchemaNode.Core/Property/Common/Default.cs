using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using SchemaNode.Relation;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Common;

/// <summary>
/// The default value
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_BOOL, SCHEMA_KIND_STRING, SCHEMA_KIND_DATE, SCHEMA_KIND_DECIMAL, SCHEMA_KIND_INT, SCHEMA_KIND_PROPERTY, SCHEMA_KIND_FUNC_ARG)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_COMMON}.{nameof(Default)}")]
[Relation<OverrideType, Call>(NODE_SELF, NODE_TYPE)]
public class Default: Property<object>;
