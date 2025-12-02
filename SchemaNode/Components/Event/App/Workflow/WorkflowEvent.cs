using SchemaNode.Context;

namespace SchemaNode.Components;

/// <summary>
/// The workflow event
/// </summary>
public class WorkflowEvent(WorkflowContext context): AppEvent(context.WorkflowType!.App)
{
    /// <summary>
    /// The topic
    /// </summary>
    public override string Topic => $"{base.Topic}/{context.WorkflowType!.Name}";
}