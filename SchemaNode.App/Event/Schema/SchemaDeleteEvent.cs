using SchemaNode.Attribute;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Event;

[Schema($"{NS_SYSTEM_EVENT}.schema.delete")]
public class SchemaDeleteEvent: SchemaEvent, IEventPayload<string>
{
}
