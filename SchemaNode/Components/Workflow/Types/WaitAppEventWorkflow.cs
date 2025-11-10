using SchemaNode.Attribute;
using SchemaNode.Context;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Components;

[SchemaType($"{NS_SYSTEM_WORKFLOW}.event.app")]
public class WaitAppEventWorkflow : EventWorkflow, 
    IWorkflowPayload, 
    IWorkflowSession<IDisposable>,
    IWorkflowState<WaitAppEventWorkflowState>
{
    public async Task<IDisposable?> ProcessAsync(WorkflowContext context, IDisposable? session = null)
    {
        await Task.Yield();
        if (Event == null) throw new Exception("Event is null");
        
        string topic = Application.Name.ToLower().Replace('.', '_');
        if (!string.IsNullOrEmpty(State.Target))
        {
            topic = $"{topic}/{State.Target}";

            if (!string.IsNullOrEmpty(State.Field))
            {
                topic = $"{topic}/{State.Field}";
            }
        }
        else if (!string.IsNullOrEmpty(State.Field))
        {
            topic = $"{topic}/+/{State.Field}";
        }
        
        session?.Dispose();
        if (Fork)
        {
            return context.SubscribeTopicEvent<ApplicationEvent>(Event!, topic, @event =>
            {
                SetPayload(context, @event.Payload);
            });
        }
        else
        {
            return context.SubscribeTopicEventOnce<ApplicationEvent>(Event!, topic, @event =>
            {
                SetPayload(context, @event.Payload);
            });
        }
    }

    /// <summary>
    /// The workflow state
    /// </summary>
    public WaitAppEventWorkflowState State { get; set; } = new();
}

[SchemaType($"{NS_SYSTEM_WORKFLOW}.event.appstate")]
public class WaitAppEventWorkflowState
{
    /// <summary>
    /// The target node
    /// </summary>
    public string? Target { get; set; }
    
    /// <summary>
    /// The field to extract from event payload
    /// </summary>
    [SchemaType(NS_SYSTEM_SCHEMA_APP_FIELD)]
    public string? Field { get; set; }
}