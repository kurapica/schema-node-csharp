using SchemaNode.Runtime;

namespace SchemaNode.Components;

public abstract class EventWorkflow: Workflow
{
    /// <summary>
    /// The event type
    /// </summary>
    public EventType? Event { get; set; }
}