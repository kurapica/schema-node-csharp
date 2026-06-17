using SchemaNode.Attribute;
using SchemaNode.Context;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Workflow;

[Schema($"{NS_SYSTEM_WORKFLOW_CONTROL}.exit")]
public class ExitWorkflow: Workflow
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