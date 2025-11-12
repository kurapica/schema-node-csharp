using SchemaNode.Attribute;
using SchemaNode.Context;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Components;

[SchemaType($"{NS_SYSTEM_WORKFLOW}.event.app")]
public class WaitAppEventWorkflow : EventWorkflow, 
    IWorkflowPayload<WaitAppEventWorkflowPayload>,
    IWorkflowSession<IDisposable>
{
    public async Task<IDisposable?> ProcessAsync(WorkflowContext context, IDisposable? session = null)
    {
        await Task.Yield();
        if (Event == null) throw new Exception("Event is null");
        
        string topic = Application.Name.ToLower().Replace('.', '_');
        
        session?.Dispose();
        if (Fork)
        {
            return context.SubscribeTopicEvent<ApplicationEvent>(Event!, topic, _ =>
            {
                SetPayload(context, new WaitAppEventWorkflowPayload
                {
                    Application = Application.Name,
                });
            });
        }
        else
        {
            return context.SubscribeTopicEventOnce<ApplicationEvent>(Event!, topic, _ =>
            {
                SetPayload(context, new WaitAppEventWorkflowPayload
                {
                    Application = Application.Name,
                });
            });
        }
    }
}


[SchemaType($"{NS_SYSTEM_WORKFLOW}.event.apppayload")]
public class WaitAppEventWorkflowPayload
{
    /// <summary>
    /// The application
    /// </summary>
    public required string Application { get; set; }
}