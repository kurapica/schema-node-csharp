using SchemaNode.Attribute;
using SchemaNode.Context;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Components;

[Schema($"{NS_SYSTEM_WORKFLOW_EVENT}.message")]
public class WaitMessageEventWorkflow : EventWorkflow, 
    IWorkflowPayload, 
    IWorkflowSession<IDisposable>
{
    public async Task<IDisposable?>  ProcessAsync(WorkflowContext context, IDisposable? session = null)
    {
        await Task.Yield();
        if (Event == null) throw new Exception("Event is null");
       
        session?.Dispose();
        return Fork 
            ? context.SubscribeEvent<Event>(Event!,  @event => SetPayload(context, @event.Payload)) 
            : context.SubscribeEventOnce<Event>(Event!, @event => SetPayload(context, @event.Payload));
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