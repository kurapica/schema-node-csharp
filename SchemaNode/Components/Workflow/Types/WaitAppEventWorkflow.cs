using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Components;

[SchemaType($"{NS_SYSTEM_WORKFLOW}.event.app")]
public class WaitAppEventWorkflow
    : EventWorkflow, IWorkflowPayload, IWorkflowSession<IDisposable>
{
    public async Task<IDisposable> ProcessAsync(WorkflowContext context, IDisposable? session = null)
    {
        await Task.Yield();
        if (Event == null) throw new Exception("Event is null");
        
        session?.Dispose();
        if (Fork)
        {
            return context.SubscribeApplicationEvent(Event.ToCSharpType(), Application, @event =>
            {
                SetPayload(context, @event.Payload);
            });
        }
        else
        {
            return context.SubscribeApplicationEventOnce(Event.ToCSharpType(), Application, @event =>
            {
                SetPayload(context, @event.Payload);
            });
        }
    }
}