using SchemaNode.Attribute;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Components;

[Schema($"{NS_SYSTEM_EVENT}.schema.appupdate")]
public class AppSchemaChangeEvent : SchemaEvent, IEventPayload<string>
{
}
