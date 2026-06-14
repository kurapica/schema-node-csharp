using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Event;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Components;

[Schema($"{NS_SYSTEM_WORKFLOW_EVENT}.appdata")]
public class WaitAppDataEventWorkflow : EventWorkflow, 
    IWorkflowPayload<WaitAppDataEventWorkflowPayload>,
    IWorkflowSession<IDisposable>
{
    public async Task<IDisposable?> ProcessAsync(WorkflowContext context, IDisposable? session = null, string? target = null)
    {
        await Task.Yield();
        if (Event == null) throw new Exception("Event is null");
        
        string topic = Application.Name.ToLower().Replace('.', '_');
        topic = !string.IsNullOrEmpty(target) ? $"{topic}/{target}" : $"{topic}/#";
        
        session?.Dispose();
        if (Fork)
        {
            return context.SubscribeTopicEvent<AppEvent>(Event!, topic, @event =>
            {
                string[] t = @event.Topic.Split("/", StringSplitOptions.RemoveEmptyEntries);
                SetPayload(context, new WaitAppDataEventWorkflowPayload
                {
                    App = Application.Name,
                    Target = t.Length > 1 ? t[1] : null,
                }, new Access { App = Application.Name, Target = t.Length > 1 ? t[1] : null });
            });
        }
        else
        {
            return context.SubscribeTopicEventOnce<AppEvent>(Event!, topic, @event =>
            {
                string[] t = @event.Topic.Split("/", StringSplitOptions.RemoveEmptyEntries);
                SetPayload(context, new WaitAppDataEventWorkflowPayload
                {
                    App = Application.Name,
                    Target = t.Length > 1 ? t[1] : null,
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

[Schema($"{NS_SYSTEM_WORKFLOW}.event.appdatapayload")]
public class WaitAppDataEventWorkflowPayload
{
    /// <summary>
    /// The application
    /// </summary>
    public required string App { get; set; }
    
    /// <summary>
    /// The target
    /// </summary>
    public string? Target { get; set; }
}