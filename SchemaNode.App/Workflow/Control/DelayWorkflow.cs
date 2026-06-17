using SchemaNode.Attribute;
using SchemaNode.Context;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Workflow;

[Schema($"{NS_SYSTEM_WORKFLOW_CONTROL}.delay")]
public class DelayWorkflow: Workflow
{
    public Task ProcessAsync(WorkflowContext context, long duration)
    {
        Task.Run(async () =>
        {
            if (duration > 0)
                await Task.Delay(TimeSpan.FromMilliseconds(duration));
            context.Done(this);
        });
        return Task.CompletedTask;
    }
}
