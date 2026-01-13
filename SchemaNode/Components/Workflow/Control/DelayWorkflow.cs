using SchemaNode.Attribute;
using SchemaNode.Context;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Components;

[Schema($"{NS_SYSTEM_WORKFLOW}.control.delay")]
public class DelayWorkflow: Workflow
{
    public Task ProcessAsync(WorkflowContext context, long duration)
    {
        Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(duration));
            context.Done(this);
        });
        return Task.CompletedTask;
    }
}
