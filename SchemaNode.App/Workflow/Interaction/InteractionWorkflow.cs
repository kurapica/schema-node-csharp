using SchemaNode.Attribute;
using SchemaNode.Context;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Workflow;

/// <summary>
/// The interaction workflow
/// </summary>
public abstract class InteractionWorkflow: BaseWorkflow
{
    /// <summary>
    /// Do nothing until the user provides
    /// </summary>
    public Task ProcessAsync(WorkflowContext context) => Task.CompletedTask;
}

/// <summary>
/// The application interaction workflow payload
/// </summary>
[Schema($"{NS_SYSTEM_WORKFLOW_INTERACTION}.payload")]
public class InteractionPayload
{
    /// <summary>
    /// The application
    /// </summary>
    public string App { get; set; } = string.Empty;
    
    /// <summary>
    /// The target
    /// </summary>
    public string Target { get; set; } = string.Empty;
}