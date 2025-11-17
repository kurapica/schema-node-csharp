using SchemaNode.Attribute;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Components;

[SchemaType($"{NS_SYSTEM_EVENT}.schema.change")]
public class SchemaChangeEvent: SchemaEvent, IEventPayload<string>
{
}
