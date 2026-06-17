using SchemaNode.Attribute;
using SchemaNode.Context;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Workflow;

[Schema($"{NS_SYSTEM_WORKFLOW_CONTROL}.goto")]
public class GotoWorkflow: BaseWorkflow
{
    public async Task ProcessAsync(WorkflowContext context, bool flag, 
        [Schema(NS_SYSTEM_WORKFLOW_NODE)] string? trueNode, 
        [Schema(NS_SYSTEM_WORKFLOW_NODE)] string falseNode)
    {
        await Task.Yield();
        context.Goto(this, flag ? trueNode : falseNode);
    }
}