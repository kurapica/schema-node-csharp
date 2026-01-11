using SchemaNode.Attribute;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Components;

/// <summary>
/// Fired when create the target field data in the application
/// </summary>
[Schema($"{NS_SYSTEM_EVENT}.app.data.create")]
public class AppFieldDataCreateEvent(AppFieldType field, string target)
    : AppFieldDataEvent(field.App, target, field.Name), IEventPayload
{
}