using SchemaNode.Attribute;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Components;

[SchemaType($"{NS_SYSTEM_EVENT}.appschema.update")]
public class AppSchemaChangeEvent : ClusterEvent, IEventPayload<string>
{
}
