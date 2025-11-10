using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Components;

[SchemaType($"{NS_SYSTEM_WORKFLOW}.control.goto")]
public class GotoWorkflow: Workflow, IWorkflowState<GotoWorkflowState>
{
    public override async Task ProcessAsync(WorkflowContext context)
    {
        await Task.Yield();
        if (string.IsNullOrEmpty(State.Flag))
            throw new Exception("Goto Workflow State is missing");

        AnySchemaNode? flagPayload = context.GetWorkflowPayload(State.Flag);
        if (flagPayload != null && flagPayload.ToValue<bool>())
        {
            context.Goto(this, State.TrueNode);
        }
        else
        {
            context.Goto(this, State.FalseNode);
        }
    }

    /// <summary>
    /// The workflow state
    /// </summary>
    public GotoWorkflowState State { get; set; } = new();
}

[SchemaType($"{NS_SYSTEM_WORKFLOW}.control.gotostate")]
public class GotoWorkflowState
{
    /// <summary>
    /// Flag result node
    /// </summary>
    [SchemaType(NS_SYSTEM_WORKFLOW_NODE)]
    public string Flag {get; set;} = string.Empty;
    
    /// <summary>
    /// Goto node if true
    /// </summary>
    [SchemaType(NS_SYSTEM_WORKFLOW_NODE)]
    public string? TrueNode { get; set; }
    
    /// <summary>
    /// Goto node if false
    /// </summary>
    [SchemaType(NS_SYSTEM_WORKFLOW_NODE)]
    public string? FalseNode { get; set; }
}