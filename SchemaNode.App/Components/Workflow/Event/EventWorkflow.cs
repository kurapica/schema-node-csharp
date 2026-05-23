using SchemaNode.Runtime;

namespace SchemaNode.Components;

/// <summary>
/// The event workflow associated with event trigger
/// </summary>
public abstract class EventWorkflow: Workflow
{
    /// <summary>
    /// The event type
    /// </summary>
    public EventType? Event { get; set; }
}