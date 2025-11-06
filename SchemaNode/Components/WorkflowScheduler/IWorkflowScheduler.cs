using SchemaNode.Context;

namespace SchemaNode.Components;

/// <summary>
/// The workflow context scheduler
/// </summary>
public interface IWorkflowScheduler
{
    /// <summary>
    /// Schedule the workflow context
    /// </summary>
    public void Schedule(WorkflowContext context);
}