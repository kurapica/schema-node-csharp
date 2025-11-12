using SchemaNode.Attribute;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Components;

/// <summary>
/// Fired when create the target field data in the application
/// </summary>
[SchemaType($"{NS_SYSTEM_EVENT}.appdata.create")]
public class AppFieldDataCreateEvent(AppFieldType field, string target): ApplicationFieldDataEvent(field.App, target, field.Name), IEventPayload
{
}