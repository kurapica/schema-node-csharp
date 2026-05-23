using SchemaNode.Attribute;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Components;

/// <summary>
/// the start interaction workflow
/// </summary>
[Schema($"{NS_SYSTEM_WORKFLOW_INTERACTION}.start")]
public class StartWorkflow: InteractionWorkflow
    , IWorkflowPayload<InteractionPayload>
{
}