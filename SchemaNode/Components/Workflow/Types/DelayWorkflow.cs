using System.Text.Json.Nodes;
using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Components;

/// <summary>
/// Delay the workflow for a period of time
/// </summary>
[SchemaType($"{NS_SYSTEM_WORKFLOW}.delay")]
public class DelayWorkflow: Workflow
{
    public Task ProcessAsync(WorkflowContext context, int delay = 10)
    {
        Task.Run(async () =>
        {
            if (delay > 0) await Task.Delay(TimeSpan.FromSeconds(delay));
            context.Done(this);
        });
        return Task.CompletedTask;
    }
}
