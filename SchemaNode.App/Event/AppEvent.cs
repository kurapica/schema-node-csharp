using SchemaNode.Attribute;
using SchemaNode.Function;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.String;
using SchemaNode.Relation;
using SchemaNode.Scalar;
using SchemaNode.Schema;
using static SchemaNode.Utility.AppConstant;
using static SchemaNode.Utility.Constant;

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
[Meta<OfNodeKind>(NODE_KIND_EVENT)]
[Meta<SchemaType>($"{NS_SYSTEM_EVENT}.app.data.create")]
[Relation<Default, Call>($"{nameof(app)}.{nameof(CallArg.Value)}", $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", APP_PROVIDER)]
[Relation<EntrySource, Assign>($"{nameof(field)}.{nameof(CallArg.Value)}", $"{NS_SYSTEM_SCHEMA_REFLECT_APP}.{nameof(SystemReflectApp.getappfields)}", $"@{nameof(app)}.{nameof(CallArg.Value)}")]
[Relation<AccessValueTypeProvider, Assign>($"{nameof(field)}.{nameof(CallArg.Value)}", $"{NS_SYSTEM_SCHEMA_REFLECT_APP}.{nameof(SystemReflectApp.getappfieldtype)}", $"@{nameof(app)}.{nameof(CallArg.Value)}", NODE_SELF, true)]
[Relation<AccessEntryConsumer, Assign>($"{nameof(field)}.{nameof(CallArg.Value)}", $"{NS_SYSTEM_SCHEMA_REFLECT_TYPE}.{nameof(Function.Reflect.Type.isgenericmatch)}", $"@{FUNC_RETURN}", $"{NS_SYSTEM_EVENT}.app.data.payload",  NODE_SELF)]
public class AppFieldDataCreateEvent<T>(
    [Meta<SchemaType>(typeof(AppType))] string app,
    [Meta<SchemaType>(typeof(Identifier))] string field,
    string? target = null) 
    : AppFieldEvent(app, field, target), IEventPayload<AppFieldPayload<T>>;

/// <summary>
/// Fired when delete the target field data in the application
/// </summary>
[Meta<OfNodeKind>(NODE_KIND_EVENT)]
[Meta<SchemaType>($"{NS_SYSTEM_EVENT}.app.data.delete")]
[Relation<Default, Call>($"{nameof(app)}.{nameof(CallArg.Value)}", $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", APP_PROVIDER)]
[Relation<EntrySource, Assign>($"{nameof(field)}.{nameof(CallArg.Value)}", $"{NS_SYSTEM_SCHEMA_REFLECT_APP}.{nameof(SystemReflectApp.getappfields)}", $"@{nameof(app)}.{nameof(CallArg.Value)}")]
[Relation<AccessValueTypeProvider, Assign>($"{nameof(field)}.{nameof(CallArg.Value)}", $"{NS_SYSTEM_SCHEMA_REFLECT_APP}.{nameof(SystemReflectApp.getappfieldtype)}", $"@{nameof(app)}.{nameof(CallArg.Value)}", NODE_SELF, true)]
[Relation<AccessEntryConsumer, Assign>($"{nameof(field)}.{nameof(CallArg.Value)}", $"{NS_SYSTEM_SCHEMA_REFLECT_TYPE}.{nameof(Function.Reflect.Type.isgenericmatch)}", $"@{FUNC_RETURN}", $"{NS_SYSTEM_EVENT}.app.data.payload", NODE_SELF)]
public class AppFieldDataDeleteEvent<T>(
    [Meta<SchemaType>(typeof(AppType))] string app,
    [Meta<SchemaType>(typeof(Identifier))] string field,
    string? target = null)
    : AppFieldEvent(app, field, target), IEventPayload<AppFieldPayload<T>>;

/// <summary>
/// Fired when update the target field data in the application
/// </summary>
[Meta<OfNodeKind>(NODE_KIND_EVENT)]
[Meta<SchemaType>($"{NS_SYSTEM_EVENT}.app.data.update")]
[Relation<Default, Call>($"{nameof(app)}.{nameof(CallArg.Value)}", $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", APP_PROVIDER)]
[Relation<EntrySource, Assign>($"{nameof(field)}.{nameof(CallArg.Value)}", $"{NS_SYSTEM_SCHEMA_REFLECT_APP}.{nameof(SystemReflectApp.getappfields)}", $"@{nameof(app)}.{nameof(CallArg.Value)}")]
[Relation<AccessValueTypeProvider, Assign>($"{nameof(field)}.{nameof(CallArg.Value)}", $"{NS_SYSTEM_SCHEMA_REFLECT_APP}.{nameof(SystemReflectApp.getappfieldtype)}", $"@{nameof(app)}.{nameof(CallArg.Value)}", NODE_SELF, true)]
[Relation<AccessEntryConsumer, Assign>($"{nameof(field)}.{nameof(CallArg.Value)}", $"{NS_SYSTEM_SCHEMA_REFLECT_TYPE}.{nameof(Function.Reflect.Type.isgenericmatch)}", $"@{FUNC_RETURN}", $"{NS_SYSTEM_EVENT}.app.data.updatepayload", NODE_SELF)]

public class AppFieldDataUpdateEvent<T>(
    [Meta<SchemaType>(typeof(AppType))] string app,
    [Meta<SchemaType>(typeof(Identifier))] string field,
    string? target = null)
    : AppFieldEvent(app, field, target), IEventPayload<AppFieldUpdatePayload<T>>;

[Meta<SchemaType>($"{NS_SYSTEM_EVENT}.app.data.payload")]
public class AppFieldPayload<T>
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
    public T? Data { get; set; }
}

[Meta<SchemaType>($"{NS_SYSTEM_EVENT}.app.data.updatepayload")]
public class AppFieldUpdatePayload<T>
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
    public T? Data { get; set; }
    
    /// <summary>
    ///  The origin data
    /// </summary>
    public T? Origin { get; set; }
}