using SchemaNode.Attribute;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Event;

/// <summary>
/// Fired when delete the target field data in the application
/// </summary>
[Schema($"{NS_SYSTEM_EVENT}.app.data.delete")]
public class AppFieldDataDeleteEvent<T>(AppFieldType field, string target) 
    : AppFieldDataEvent(field.App, target, field.Name), IEventPayload<T>
{
}