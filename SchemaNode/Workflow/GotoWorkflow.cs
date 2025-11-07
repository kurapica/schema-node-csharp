using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Components;

[SchemaType($"{NS_SYSTEM_WORKFLOW}.control.goto")]
public class GotoWorkflow([SchemaType(NS_SYSTEM_WORKFLOW_NODE)]string flag, 
    [SchemaType(NS_SYSTEM_WORKFLOW_NODE)]string? trueNode,
    [SchemaType(NS_SYSTEM_WORKFLOW_NODE)]string? falseNode): Workflow
{
    public override async Task ProcessAsync(WorkflowContext context)
    {
        await Task.Yield();

        AnySchemaNode? flagPayload = context.GetWorkflowPayload(flag);
        if (flagPayload != null && flagPayload.ToValue<bool>())
        {
            context.Goto(this, trueNode);
        }
        else
        {
            context.Goto(this, falseNode);
        }
    }
}