using SchemaNode.Attribute;
using SchemaNode.Context;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Workflow;

/// <summary>
/// Break Branch BaseWorkflow
/// </summary>
[Schema($"{NS_SYSTEM_WORKFLOW_CONTROL}.break")]
public class BreakWorkflow: BaseWorkflow
{
    public Task ProcessAsync(WorkflowContext context, bool cond)
    {
        if (cond)
        {
            context.Terminate(this);
        }
        else
        {
            context.Done(this);
        }
        return Task.CompletedTask;
    }
}