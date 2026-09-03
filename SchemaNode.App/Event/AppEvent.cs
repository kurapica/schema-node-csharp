using SchemaNode.Attribute;
using SchemaNode.Function;
using SchemaNode.Node;
using SchemaNode.Property.Core;
using SchemaNode.Property.Event;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;

namespace SchemaNode.Event;

/// <summary>
/// The application scope event
/// </summary>
public abstract class AppEvent(string app, string? target = null): BaseEvent
{
    /// <summary>
    /// The application
    /// </summary>
    public string App => app;
    
    /// <summary>
    /// The application target
    /// </summary>
    public string? Target => target;
    
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
public abstract class AppFieldEvent(string app, string field, string? target = null): AppEvent(app, target)
{
    /// <summary>
    /// The topic
    /// </summary>
    public override string Topic => Target != null ? $"{base.Topic}/{@field}/{Target}"  : $"{base.Topic}/{@field}";

    /// <summary>
    /// The match topic
    /// </summary>
    public override string MatchTopic => Target != null ? $"{base.Topic}/{@field}/{Target}" : $"{base.Topic}/{@field}/*";
}

/// <summary>
/// Fired when create the target field data in the application
/// </summary>
[Meta<OfSchema>(SCHEMA_KIND_EVENT)]
[Meta<SchemaType>($"{NS_SYSTEM_EVENT}.app.data.create")]
[Meta<PayloadEvaluator>($"{NS_SYSTEM_SCHEMA_REFLECT}.event.{nameof(SystemReflectEvent.getappfieldpayload)}")]
public class AppFieldDataCreateEvent(string app, string field, string? target = null) 
    : AppFieldEvent(app, field, target), IEventPayload<AppFieldPayload>;

/// <summary>
/// Fired when delete the target field data in the application
/// </summary>
[Meta<OfSchema>(SCHEMA_KIND_EVENT)]
[Meta<SchemaType>($"{NS_SYSTEM_EVENT}.app.data.delete")]
[Meta<PayloadEvaluator>($"{NS_SYSTEM_SCHEMA_REFLECT}.event.{nameof(SystemReflectEvent.getappfieldpayload)}")]
public class AppFieldDataDeleteEvent(string app, string field, string? target = null) 
    : AppFieldEvent(app, field, target), IEventPayload<AppFieldPayload>;

/// <summary>
/// Fired when update the target field data in the application
/// </summary>
[Meta<OfSchema>(SCHEMA_KIND_EVENT)]
[Meta<SchemaType>($"{NS_SYSTEM_EVENT}.app.data.update")]
[Meta<PayloadEvaluator>($"{NS_SYSTEM_SCHEMA_REFLECT}.event.{nameof(SystemReflectEvent.getappfieldupdatepayload)}")]
public class AppFieldDataUpdateEvent(string app, string field, string? target = null) 
    : AppFieldEvent(app, field, target), IEventPayload<AppFieldUpdatePayload>;

[Meta<SchemaType>($"{NS_SYSTEM_EVENT}.app.data.payload")]
[Meta<Generics>(NS_GENERIC_TYPE)]
public class AppFieldPayload
{
    /// <summary>
    /// The application triggered the event
    /// </summary>
    public string App { get; set; } = null!;
    
    /// <summary>
    /// The event target if existed
    /// </summary>
    public string? Target { get; set; }
    
    /// <summary>
    /// The application field that triggered the event
    /// </summary>
    public string Field { get; set; } = null!;
    
    /// <summary>
    /// The event data
    /// </summary>
    [Meta<SchemaType>(NS_GENERIC_TYPE)]
    public IValueAccess? Data { get; set; }
}

[Meta<SchemaType>($"{NS_SYSTEM_EVENT}.app.data.updatepayload")]
[Meta<Generics>(NS_GENERIC_TYPE)]
public class AppFieldUpdatePayload
{
    /// <summary>
    /// The application triggered the event
    /// </summary>
    public string App { get; set; } = null!;
    
    /// <summary>
    /// The event target if existed
    /// </summary>
    public string? Target { get; set; }
    
    /// <summary>
    /// The application field that triggered the event
    /// </summary>
    public string Field { get; set; } = null!;
    
    /// <summary>
    /// The event data
    /// </summary>
    [Meta<SchemaType>(NS_GENERIC_TYPE)]
    public IValueAccess? Data { get; set; }
    
    /// <summary>
    ///  The origin data
    /// </summary>
    [Meta<SchemaType>(NS_GENERIC_TYPE)]
    public IValueAccess? Origin { get; set; }
}