using System.Text.Json.Nodes;
using SchemaNode.Context;

namespace SchemaNode.Components;

/// <summary>
/// The workflow base class
/// </summary>
public abstract class Workflow
{
    /// <summary>
    /// Process the workflow
    /// </summary>
    public abstract Task ProcessAsync(WorkflowContext context);

    /// <summary>
    /// Done the workflow with result
    /// </summary>
    public void Done(WorkflowContext context, JsonNode? result)
    {
        
    }
    
    /// <summary>
    /// Failed the workflow with exception
    /// </summary>
    /// <param name="context"></param>
    /// <param name="ex"></param>
    public void Error(WorkflowContext context, Exception ex)
    {
        
    }
}