namespace SchemaNode.Components;

/// <summary>
/// The workflow scope event
/// </summary>
public abstract class WorkflowEvent: Event
{
    /// <summary>
    /// The workflow identifier
    /// </summary>
    public Guid WorkflowId { get; set; }
}

/// <summary>
/// The workflow event dispatcher
/// </summary>
public interface IWorkflowEventDispatcher: IEventDispatcher<WorkflowEvent>
{
}