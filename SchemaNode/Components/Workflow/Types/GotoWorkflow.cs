using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Components;

[SchemaType($"{NS_SYSTEM_WORKFLOW}.control.goto")]
public class GotoWorkflow: Workflow
{
    public async Task ProcessAsync(WorkflowContext context, bool flag, 
        [SchemaType(NS_SYSTEM_WORKFLOW_NODE)] string? trueNode, 
        [SchemaType(NS_SYSTEM_WORKFLOW_NODE)] string falseNode)
    {
        await Task.Yield();

        if (flag)
        {
            context.Goto(this, trueNode);
        }
        else
        {
            context.Goto(this, falseNode);
        }
    }
}