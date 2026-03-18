using SchemaNode.Attribute;
using SchemaNode.Context;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Components;

[Schema($"{NS_SYSTEM_WORKFLOW_EVENT}.app")]
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
            return context.SubscribeTopicEvent<AppEvent>(Event!, topic, _ =>
            {
                SetPayload(context, new WaitAppEventWorkflowPayload{
                    App = Application.Name
                }, new Access { App = Application.Name });
            });
        }
        else
        {
            return context.SubscribeTopicEventOnce<AppEvent>(Event!, topic, _ =>
            {
                SetPayload(context, new WaitAppEventWorkflowPayload{ 
                    App = Application.Name 
                });
            });
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


[Schema($"{NS_SYSTEM_WORKFLOW}.event.apppayload")]
public class WaitAppEventWorkflowPayload
{
    /// <summary>
    /// The application
    /// </summary>
    public required string App { get; set; }
}