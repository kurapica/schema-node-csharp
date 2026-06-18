using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.AppConstant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Workflow;

[Meta<SchemaType>($"{NS_SYSTEM_WORKFLOW_CONTROL}.delay")]
public class DelayWorkflow: BaseWorkflow
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
