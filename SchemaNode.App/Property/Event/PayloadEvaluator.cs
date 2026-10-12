using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using SchemaNode.Property.Property;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;

namespace SchemaNode.Property.Event;

/// <summary>
/// The event payload evaluator based on event arguments
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_NODE_EVENT)]
[Meta<OfNodeKind>(NODE_KIND_PROPERTY)]
[Meta<PropertyValueType>(typeof(TypeFuncType))]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP}.event.{nameof(PayloadEvaluator)}")]
public class PayloadEvaluator : Property<string>;