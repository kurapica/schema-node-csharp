using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.AppConstant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Workflow;

[Meta<SchemaType>($"{NS_SYSTEM_WORKFLOW_CONTROL}.goto")]
public class GotoWorkflow: BaseWorkflow
{
    public async Task ProcessAsync(WorkflowContext context, bool flag, 
        [Meta<SchemaType>(typeof(NodeName))] string? trueNode, 
        [Meta<SchemaType>(typeof(NodeName))] string falseNode)
    {
        await Task.Yield();
        context.Goto(this, flag ? trueNode : falseNode);
    }
}