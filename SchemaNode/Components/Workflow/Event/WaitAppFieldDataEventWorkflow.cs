using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Components;

[Schema($"{NS_SYSTEM_WORKFLOW}.event.appfielddata")]
public class WaitAppFieldDataEventWorkflow : EventWorkflow, 
    IWorkflowPayload<WaitAppFieldDataEventWorkflowPayload>,
    IWorkflowSession<IDisposable>
{
    public async Task<IDisposable?> ProcessAsync(WorkflowContext context, IDisposable? session = null,
        [Schema(NS_SYSTEM_SCHEMA_APP_FIELD)] string field = "", 
        string? target = null)
    {
        await Task.Yield();
        if (Event == null) throw new Exception("Event is null");
        
        string topic = Application.Name.ToLower().Replace('.', '_');
        if (!string.IsNullOrEmpty(target))
        {
            topic = $"{topic}/{target}";

            if (!string.IsNullOrEmpty(field))
            {
                topic = $"{topic}/{field}";
            }
        }
        else if (!string.IsNullOrEmpty(field))
        {
            topic = $"{topic}/+/{field}";
        }
        
        session?.Dispose();

        // normally should be forked
        if (Fork)
        {
            return context.SubscribeTopicEvent<AppEvent>(Event!, topic, @event =>
            {
                string[] t = @event.Topic.Split("/", StringSplitOptions.RemoveEmptyEntries);
                SetPayload(context, new WaitAppFieldDataEventWorkflowPayload
                {
                    App = Application.Name,
                    Field = t.Length > 2 ? t[2] : null,
                    Target = t.Length > 1 ? t[1] : null,
                    Data = @event.Payload,
                    Origin = @event.Payload?.Origin
                }, new Access { App = Application.Name, Target = t.Length > 1 ? t[1] : null });
            });
        }
        else
        {
            return context.SubscribeTopicEventOnce<AppEvent>(Event!, topic, @event =>
            {
                string[] t = @event.Topic.Split("/", StringSplitOptions.RemoveEmptyEntries);
                SetPayload(context, new WaitAppFieldDataEventWorkflowPayload
                {
                    App = Application.Name,
                    Field = t.Length > 2 ? t[2] : null,
                    Target = t.Length > 1 ? t[1] : null,
                    Data = @event.Payload,
                    Origin = @event.Payload?.Origin
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

[Schema($"{NS_SYSTEM_WORKFLOW}.event.appfielddatapayload")]
public class WaitAppFieldDataEventWorkflowPayload
{
    /// <summary>
    /// The application
    /// </summary>
    public required string App { get; set; }
    
    /// <summary>
    /// The field
    /// </summary>
    public string? Field { get; set; }
    
    /// <summary>
    /// The target
    /// </summary>
    public string? Target { get; set; }
    
    /// <summary>
    /// The event data
    /// </summary>
    [Schema(NS_GENERIC_TYPE)]
    public AnySchemaNode? Data { get; set; }
    
    /// <summary>
    /// The origin data
    /// </summary>
    [Schema(NS_GENERIC_TYPE)]
    public AnySchemaNode? Origin { get; set; }
}