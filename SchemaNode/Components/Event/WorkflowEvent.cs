using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchemaNode.Context;
using SchemaNode.Enum;

namespace SchemaNode.Components;

/// <summary>
/// The workflow scope event with payload
/// </summary>
public abstract class WorkflowEvent<T>: Event<T>
{
    /// <summary>
    /// The event scope
    /// </summary>
    public override EventScope Scope => EventScope.Workflow;
    
    /// <summary>
    /// The workflow identifier
    /// </summary>
    public Guid WorkflowId { get; set; }
    
    /// <summary>   
    /// Raise the workflow event
    /// </summary>
    public void Raise(WorkflowContext context)
    {
        WorkflowId = context.WorkflowId;
        
        IWorkflowEventScheduler? handler = context.ServiceProvider.GetService<IWorkflowEventScheduler>();
        handler?.Schedule(this);
        
        context.Logger.LogInformation("[WorkflowEvent]{EventId} [Topic]{Topic} [Workflow]{WorkflowId} [Payload]{Payload}",
            Id, WorkflowId, Topic, Payload);
    }
}