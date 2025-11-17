using SchemaNode.Attribute;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Components;

[SchemaType($"{NS_SYSTEM_EVENT}.schema.delete")]
public class SchemaDeleteEvent: SchemaEvent, IEventPayload<string>
{
}
