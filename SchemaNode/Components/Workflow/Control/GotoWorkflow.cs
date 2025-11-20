using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Components;

[Schema($"{NS_SYSTEM_WORKFLOW}.control.goto")]
public class GotoWorkflow: Workflow
{
    public async Task ProcessAsync(WorkflowContext context, bool flag, 
        [Schema(NS_SYSTEM_WORKFLOW_NODE)] string? trueNode, 
        [Schema(NS_SYSTEM_WORKFLOW_NODE)] string falseNode)
    {
        await Task.Yield();
        context.Goto(this, flag ? trueNode : falseNode);
    }
}