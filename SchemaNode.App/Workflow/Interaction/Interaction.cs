using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Property.Core;
using SchemaNode.Property.Record;
using SchemaNode.Property.Workflow;
using static SchemaNode.Utility.AppConstant;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Workflow;

/// <summary>
/// The interaction workflow
/// </summary>
[Meta<WorkflowKind>(WORKFLOW_KIND_INTERACTION)]
[Meta<SchemaType>($"{NS_SYSTEM_WORKFLOW}.interaction")]
[Meta<OfNodeKind>(NODE_KIND_WORKFLOW)]
[Meta<Forkable>(true)]
public class Interaction<T>: BaseWorkflow, IWorkflowPayload<T>
{
    /// <summary>
    /// Do nothing until the user provides
    /// </summary>
    public Task ProcessAsync(WorkflowContext context) => Task.CompletedTask;
}