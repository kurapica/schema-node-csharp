using System.Reactive.Disposables;
using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Components;

[SchemaType($"{NS_SYSTEM_WORKFLOW}.event.field")]
public class WaitAppFieldEventWorkflow([SchemaType(NS_SYSTEM_SCHEMA_APP_FIELD)]string field)
    : EventWorkflow, IWorkflowPayload, IWorkflowSession<IDisposable>
{
    public async Task<IDisposable> ProcessAsync(WorkflowContext context, IDisposable? session = null)
    {
        await Task.Yield();
        if (Event == null) throw new Exception("Event is null");
        
        session?.Dispose();
        IDisposable sub = Disposable.Empty;
        sub = context.SubscribeApplicationEvent(Event.ToCSharpType(), Application, @event =>
        {
            if (!field.Equals(@event.Field, StringComparison.OrdinalIgnoreCase)) return;
            if (!Fork) sub.Dispose();
            SetPayload(context, @event.Payload);
        });
        return sub;
    }
}