using SchemaNode.Attribute;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Components;

[SchemaType($"{NS_SYSTEM_EVENT}.appschema.delete")]
public class AppSchemaDeleteEvent : ClusterEvent, IEventPayload<string>
{
}
