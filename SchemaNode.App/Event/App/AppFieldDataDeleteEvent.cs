using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using static SchemaNode.Utility.AppConstant;

namespace SchemaNode.Event;

/// <summary>
/// Fired when delete the target field data in the application
/// </summary>
[Meta<OfSchema>(SCHEMA_KIND_EVENT)]
[Meta<SchemaType>($"{NS_SYSTEM_EVENT}.app.data.delete")]
public class AppFieldDataDeleteEvent<T>(AppFieldType field, string target) 
    : AppFieldDataEvent(field.App, target, field.Name), IEventPayload<T>;