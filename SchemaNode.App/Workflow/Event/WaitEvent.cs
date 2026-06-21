using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Event;
using SchemaNode.Function;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Record;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;
using EventType = SchemaNode.Schema.EventType;
using Object = SchemaNode.Scalar.Object;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Workflow;

[Meta<WorkflowKind>(WORKFLOW_KIND_EVENT)]
[Meta<OfSchema>(SCHEMA_KIND_WORKFLOW)]
[Meta<SchemaType>($"{NS_SYSTEM_WORKFLOW}.waitevent")]
public class WaitEvent : BaseWorkflow, 
    IWorkflowPayload<Object>, 
    IWorkflowSession<IDisposable>
{
    private Runtime.EventType? _eventType;
    private CallArg[] _args = [];
    
    public override async Task LoadAsync(SchemaContext context, AppWorkflowNodeSchema schema)
    {
        EventDeclare? decare = schema.GetProperty<EventProperty>()?.Value;
        if (decare == null)
        {
            schema.Error ??= AppErrorCodes.WORKFLOW_EVENT_NOT_VALID;
            return;
        }
        _eventType = await context.GetNodeTypeAsync<Runtime.EventType>(decare.Event);
        if (_eventType == null)
        {
            schema.Error ??= AppErrorCodes.WORKFLOW_EVENT_NOT_VALID;
            return;
        }
        _args = decare.Args;
    }
    
    public async Task<IDisposable?> ProcessAsync(WorkflowContext context, IDisposable? session = null)
    {
        session?.Dispose();
        
        await Task.Yield();
        if (_eventType == null)
        {
            context.Error(this, $"The event is not defined.");
            return null;
        }
        
        var instance = await _eventType.GetEventInstance(context, 
            _args.Select<CallArg, object?>(callArg => string.IsNullOrEmpty(callArg.Source)
                ? callArg.Value?.DeepClone()
                : context.GetWorkflowPayload(callArg.Source)).ToArray());
        if (instance == null)
        {
            context.Error(this, $"The event {_eventType.Name} can't be instantiated.");
            return null;
        }
        
        // special for app event, add access chang
        if (instance is AppEvent)
        {
            // use topic or not
            if (!string.IsNullOrWhiteSpace(instance.MatchTopic))
            {
                return Fork
                    ? context.SubscribeTopicEvent<AppEvent>(instance.GetType(), instance.MatchTopic, HandleAppEvent)
                    : context.SubscribeTopicEventOnce<AppEvent>(instance.GetType(), instance.MatchTopic, HandleAppEvent);
            }
        
            return Fork 
                ? context.SubscribeEvent<AppEvent>(instance.GetType(), HandleAppEvent)
                : context.SubscribeEventOnce<AppEvent>(instance.GetType(), HandleAppEvent);

        }

        // use topic or not
        if (!string.IsNullOrWhiteSpace(instance.MatchTopic))
        {
            return Fork
                ? context.SubscribeTopicEvent<BaseEvent>(instance.GetType(), instance.MatchTopic, HandleEvent)
                : context.SubscribeTopicEventOnce<BaseEvent>(instance.GetType(), instance.MatchTopic, HandleEvent);
        }
        
        return Fork 
            ? context.SubscribeEvent<BaseEvent>(instance.GetType(), HandleEvent)
            : context.SubscribeEventOnce<BaseEvent>(instance.GetType(), HandleEvent);

        void HandleEvent(BaseEvent @event)
        {
            SetPayload(context, @event.Payload);
        }

        void HandleAppEvent(AppEvent @event)
        {
            SetPayload(context, @event.Payload, new Access{ App = @event.App, Target = @event.Target });
        }
    }
    
    /// <summary>
    /// Release the subscription
    /// </summary>
    public Task ReleaseSessionAsync(WorkflowContext context, IDisposable? session)
    {
        session?.Dispose();
        return Task.CompletedTask;
    }
}

[Meta<ForSchema>(SCHEMA_KIND_APP_WORKFLOW_NODE)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY}.workflow.event")]
[Relation<Visible>($"{NS_SYSTEM_SCHEMA_REFLECT}.workflow.{nameof(SystemAppReflect.Workflow.iskind)}", $"${nameof(AppWorkflowNodeSchema.Type)}", WORKFLOW_KIND_EVENT)]
public class EventProperty : Property<EventDeclare>;

/// <summary>
/// The event declare
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_COMMON}.{nameof(EventDeclare)}")]
public class EventDeclare
{
    /// <summary>
    /// The function name
    /// </summary>
    [Meta<SchemaType>(typeof(EventType))]
    public string Event { get; set; } = string.Empty;

    /// <summary>
    /// The event arguments
    /// </summary>
    public CallArg[] Args { get; set; } = [];
}