namespace SchemaNode.Components;

/// <summary>
/// The workflow scope event
/// </summary>
public abstract class WorkflowEvent(Guid workflowId): Event
{
    /// <summary>
    /// The workflow identifier
    /// </summary>
    public Guid WorkflowId { get; set; } = workflowId;
}

/// <summary>
/// The workflow event dispatcher
/// </summary>
public interface IWorkflowEventDispatcher: IEventDispatcher<WorkflowEvent>
{
}