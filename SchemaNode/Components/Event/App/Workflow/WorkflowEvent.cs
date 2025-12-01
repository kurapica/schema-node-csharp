using SchemaNode.Context;

namespace SchemaNode.Components;

public class WorkflowEvent(WorkflowContext context): AppEvent(context.Workflow!.App)
{
    /// <summary>
    /// The topic
    /// </summary>
    public override string Topic => $"{base.Topic}/{context.Workflow.Name}";
}
