using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Components;

/// <summary>
/// The interaction workflow
/// </summary>
public abstract class InteractionWorkflow: Workflow,
    IWorkflowPayload<InteractionWorkflowPayload>
{
    /// <summary>
    /// The form type if not the payload type not match
    /// </summary>
    public AnySchemeType? FormType { get; set; }
    
    /// <summary>
    /// Do nothing until the user provides
    /// </summary>
    public Task ProcessAsync(WorkflowContext context) => Task.CompletedTask;
}

/// <summary>
/// The application interaction workflow payload
/// </summary>
public class InteractionWorkflowPayload
{
    /// <summary>
    /// The application
    /// </summary>
    public required string App { get; set; }
    
    /// <summary>
    /// The target
    /// </summary>
    public required string Target { get; set; }
    
    /// <summary>
    /// The workflow name
    /// </summary>
    public required string Workflow { get; set; }
    
    /// <summary>
    /// The workflow node name
    /// </summary>
    public required string Node { get; set; }
    
    /// <summary>
    /// If the start node is not the first node, the workflow id should be provided
    /// </summary>
    public Guid? WorkflowId { get; set; }
    
    /// <summary>
    /// The event data
    /// </summary>
    [Schema(NS_GENERIC_TYPE)]
    public AnySchemaNode? Data { get; set; }
}