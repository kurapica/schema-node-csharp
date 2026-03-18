using SchemaNode.Api.Schema.Application;
using SchemaNode.Attribute;
using SchemaNode.Context;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Components;

/// <summary>
/// Raise an interaction workflow
/// </summary>
[Schema($"{NS_SYSTEM_WORKFLOW_CONTROL}.interaction")]
public class RaiseInteractionWorkflow: Workflow
{
    /// <summary>
    /// Start the interaction workflow
    /// </summary>
    public async Task ProcessAsync(WorkflowContext context, 
        [Schema(NS_SYSTEM_WORKFLOW_ID)] string workflow, 
        InteractionPayload payload)
    {
        await context.InteractionAsync(new InteractionRequest
        {
            Workflow = workflow,
            App = payload.App,
            Target = payload.Target
        }, true);

        context.Done(this);
    }
}