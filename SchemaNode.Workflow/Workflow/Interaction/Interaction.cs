using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Property.Workflow;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.WorkflowConstant;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Workflow;

/// <summary>
/// The interaction workflow
/// </summary>
[Meta<Property.Workflow.WorkflowKind>(WORKFLOW_KIND_INTERACTION)]
[Meta<SchemaType>($"{NS_SYSTEM_WORKFLOW}.interaction")]
[Meta<OfNodeKind>(NODE_KIND_WORKFLOW_NODE)]
[Meta<Forkable>(true)]
public class Interaction<T>: BaseWorkflow, IWorkflowPayload<T>
{
    /// <summary>
    /// Do nothing until the user provides
    /// </summary>
    public Task ProcessAsync(WorkflowContext context) => Task.CompletedTask;
}