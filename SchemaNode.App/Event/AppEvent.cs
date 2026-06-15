using SchemaNode.Attribute;
using SchemaNode.Node;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using static SchemaNode.Utility.AppConstant;

namespace SchemaNode.Event;

/// <summary>
/// The application scope event
/// </summary>
public abstract class AppEvent(string app): Event
{
    /// <summary>
    /// The topic
    /// </summary>
    public override string Topic => app.ToLower().Replace(".", "_");
}


/// <summary>
/// The application data event, normally for target app data access
/// </summary>
/// <param name="app"></param>
/// <param name="target"></param>
public abstract class AppDataEvent(string app, string? target): AppEvent(app)
{
    /// <summary>
    /// The topic
    /// </summary>
    public override string Topic => $"{base.Topic}/{target}";
}

/// <summary>
/// Fired when create the target field data in the application
/// </summary>
[Meta<OfSchema>(SCHEMA_KIND_EVENT)]
[Meta<SchemaType>($"{NS_SYSTEM_EVENT}.app.data.create")]
public class AppFieldDataCreateEvent(AppFieldType field, string target)
    : AppFieldDataEvent(field.App, target, field.Name), IEventPayload<AppFieldEventData>;
    
    
/// <summary>
/// Fired when delete the target field data in the application
/// </summary>
[Meta<OfSchema>(SCHEMA_KIND_EVENT)]
[Meta<SchemaType>($"{NS_SYSTEM_EVENT}.app.data.delete")]
public class AppFieldDataDeleteEvent(AppFieldType field, string target) 
    : AppFieldDataEvent(field.App, target, field.Name), IEventPayload<AppFieldEventData>;
    
    
/// <summary>
/// The application field data event, normally for specific field data update
/// </summary>
/// <param name="app"></param>
/// <param name="target"></param>
/// <param name="field"></param>
public abstract class AppFieldDataEvent(string app, string target, string @field): AppDataEvent(app, target)
{
    public override string Topic => $"{base.Topic}/{@field}";
}

/// <summary>
/// Fired when update the target field data in the application
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_EVENT}.app.data.update")]
public class AppFieldDataUpdateEvent(AppFieldType field, string target) : AppFieldDataEvent(field.App, target, field.Name), IEventPayload<AppFieldEventData>;

[Meta<SchemaType>($"{NS_SYSTEM_EVENT}.app.data")]
public class AppEventData
{
    /// <summary>
    /// The application triggered the event
    /// </summary>
    public string App { get; set; } = null!;
    
    /// <summary>
    /// The event target if existed
    /// </summary>
    public string? Target { get; set; }
}

[Meta<SchemaType>($"{NS_SYSTEM_EVENT}.app.field.data")]
[Meta<Generics>("T")]
public class AppFieldEventData : AppEventData
{
    /// <summary>
    /// The application field that triggered the event
    /// </summary>
    public string Field { get; set; } = null!;
    
    /// <summary>
    /// The event data
    /// </summary>
    [Meta<SchemaType>("T")]
    public DataNode? Data { get; set; }
    
    /// <summary>
    ///  The origin data
    /// </summary>
    [Meta<SchemaType>("T")]
    public DataNode? Origin { get; set; }
}