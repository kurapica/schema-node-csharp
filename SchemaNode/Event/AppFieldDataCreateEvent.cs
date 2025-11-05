using SchemaNode.Attribute;
using SchemaNode.Node;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Components;

/// <summary>
/// Fired when create the target field data in the application
/// </summary>
[SchemaType($"{NS_SYSTEM_EVENT}.app.data.create")]
public class AppFieldDataCreateEvent: ApplicationEvent, IEventPayload<AnySchemaNode>
{
    public AnySchemaNode? Payload { get; set; }
    public AnySchemaNode? Origin { get; set; }
}