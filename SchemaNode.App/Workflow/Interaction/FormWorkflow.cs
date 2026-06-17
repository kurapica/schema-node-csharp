using SchemaNode.Attribute;
using SchemaNode.Node;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Workflow;

/// <summary>
/// The form interaction workflow
/// </summary>
[Schema($"{NS_SYSTEM_WORKFLOW_INTERACTION}.form")]
public class FormWorkflow: InteractionWorkflow,
    IWorkflowPayload<FormInteractionPayload>
{
}

/// <summary>
/// The form interaction workflow payload
/// </summary>
/// <typeparam name="T"></typeparam>
[Schema($"{NS_SYSTEM_WORKFLOW_INTERACTION}.formpayload")]
public class FormInteractionPayload: InteractionPayload
{
    /// <summary>
    /// The event data
    /// </summary>
    [Schema(NS_GENERIC_TYPE)]
    public DataNode? Data { get; set; }
}