using SchemaNode.Attribute;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Components;

[SchemaType($"{NS_SYSTEM_EVENT}.schema.appdelete")]
public class AppSchemaDeleteEvent : SchemaEvent, IEventPayload<string>
{
}
