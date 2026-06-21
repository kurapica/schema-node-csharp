using SchemaNode.Attribute;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Components;

/// <summary>
/// Fired when update the target field data in the application
/// </summary>
[Schema($"{NS_SYSTEM_EVENT}.app.data.update")]
public class AppFieldDataUpdateEvent(AppFieldType field, string target) 
    : AppFieldDataEvent(field.App, target, field.Name), IEventPayload
{
}