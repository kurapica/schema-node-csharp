using SchemaNode.Attribute;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Event;

[Schema($"{NS_SYSTEM_EVENT}.schema.appdelete")]
public class AppSchemaDeleteEvent : SchemaEvent, IEventPayload<string>
{
}
