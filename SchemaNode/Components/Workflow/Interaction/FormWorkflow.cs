using SchemaNode.Attribute;
using SchemaNode.Node;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Components;

/// <summary>
/// The form interaction workflow
/// </summary>
[Schema($"{NS_SYSTEM_WORKFLOW}.interaction.form")]
public class FormWorkflow: InteractionWorkflow,
    IWorkflowPayload<FormInteractionPayload<AnySchemeType>>
{
}

/// <summary>
/// The form interaction workflow payload
/// </summary>
/// <typeparam name="T"></typeparam>
[Schema($"{NS_SYSTEM_WORKFLOW}.interaction.formpayload")]
public class FormInteractionPayload<T>: InteractionPayload
{
    /// <summary>
    /// The event data
    /// </summary>
    [Schema(NS_GENERIC_TYPE)]
    public AnySchemaNode? Data { get; set; }
}