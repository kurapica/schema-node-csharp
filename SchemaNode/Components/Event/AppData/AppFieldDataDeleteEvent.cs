using SchemaNode.Attribute;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Components;

/// <summary>
/// Fired when delete the target field data in the application
/// </summary>
[SchemaType($"{NS_SYSTEM_EVENT}.appdata.delete")]
public class AppFieldDataDeleteEvent(AppFieldType field, string target) : ApplicationFieldDataEvent(field.App, target, field.Name), IEventPayload
{
}