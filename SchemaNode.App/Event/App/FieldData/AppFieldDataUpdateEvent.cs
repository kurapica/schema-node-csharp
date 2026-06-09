using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using static SchemaNode.Utility.AppConstant;

namespace SchemaNode.Event;

/// <summary>
/// Fired when update the target field data in the application
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_EVENT}.app.data.update")]
public class AppFieldDataUpdateEvent(AppFieldType field, string target) : AppFieldDataEvent(field.App, target, field.Name), IEventPayload;

/// <summary>
/// The data update payload
/// </summary>
public record AppFieldDataUpdatePayload<T>(T Data, T Origin);