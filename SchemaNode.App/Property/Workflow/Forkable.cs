using SchemaNode.Attribute;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Property;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;

namespace SchemaNode.Property.Workflow;

/// <summary>
/// The workflow is forkable
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_WORKFLOW)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_APP}.workflow.{nameof(Forkable)}")]
[Meta<Static>(true)]
[Meta<ReadOnly>(true)]
public class Forkable: Property<Boolean>;