using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.AppConstant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Workflow;

/// <summary>
/// Break Branch BaseWorkflow
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_WORKFLOW_CONTROL}.break")]
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