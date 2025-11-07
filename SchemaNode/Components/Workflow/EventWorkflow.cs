using SchemaNode.Runtime;
using SchemaNode.Schema;

namespace SchemaNode.Components;

public abstract class EventWorkflow: Workflow
{
    /// <summary>
    /// The event type
    /// </summary>
    public EventType? Event { get; set; }
}