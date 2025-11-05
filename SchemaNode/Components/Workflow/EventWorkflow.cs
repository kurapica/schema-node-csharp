using SchemaNode.Runtime;
using SchemaNode.Schema;

namespace SchemaNode.Components;

public abstract class EventWorkflow: Workflow
{
    /// <summary>
    /// The given event
    /// </summary>
    public EventType Event { get; set; } = null!;
    
    /// <summary>
    /// The event arguments
    /// </summary>
    public FuncCallArg[] Args { get; set; } = [];
}