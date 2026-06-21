using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Property.Core;
using Object = SchemaNode.Scalar.Object;
using static SchemaNode.Utility.AppConstant;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Workflow;

/// <summary>
/// The interaction workflow
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_WORKFLOW}.{nameof(Interaction)}")]
[Meta<OfSchema>(SCHEMA_KIND_WORKFLOW)]
public class Interaction: BaseWorkflow, IWorkflowPayload<Object>
{
    /// <summary>
    /// Do nothing until the user provides
    /// </summary>
    public Task ProcessAsync(WorkflowContext context) => Task.CompletedTask;
}