using SchemaNode.Runtime;

namespace SchemaNode.Workflow;

/// <summary>
/// The event workflow associated with event trigger
/// </summary>
public abstract class EventWorkflow: BaseWorkflow
{
    /// <summary>
    /// The event type
    /// </summary>
    public EventType? Event { get; set; }
}