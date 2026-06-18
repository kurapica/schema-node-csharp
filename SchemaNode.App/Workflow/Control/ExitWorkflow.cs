using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.AppConstant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Workflow;

[Meta<SchemaType>($"{NS_SYSTEM_WORKFLOW_CONTROL}.exit")]
public class ExitWorkflow: BaseWorkflow
{
    public async Task ProcessAsync(WorkflowContext context, bool cond)
    {
        if (cond)
        {
            await context.TerminateAsync();
        }
        else
        {
            context.Done(this);
        }
    }
}