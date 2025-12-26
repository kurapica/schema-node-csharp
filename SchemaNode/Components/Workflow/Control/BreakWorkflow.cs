using SchemaNode.Attribute;
using SchemaNode.Context;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Components;

/// <summary>
/// Break Branch Workflow
/// </summary>
[Schema($"{NS_SYSTEM_WORKFLOW}.control.break")]
public class BreakWorkflow: Workflow
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