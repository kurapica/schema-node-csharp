using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Schema;
using SchemaNode.Utility;
// ReSharper disable SuspiciousTypeConversion.Global

namespace SchemaNode.Components;

/// <summary>
/// The workflow base class
/// </summary>
public abstract class Workflow
{
    #region Properties
    
    /// <summary>
    /// The workflow name
    /// </summary>
    internal string Name { get; set; } = string.Empty;

    /// <summary>
    /// The workflow context
    /// </summary>
    internal WorkflowContext Context { get; set; } = default!;
    
    /// <summary>
    /// The previous workflows
    /// </summary>
    internal Workflow[]? Previous { get; set; }
    
    /// <summary>
    /// The next workflows
    /// </summary>
    internal Workflow[]? Next { get; set; }
    
    /// <summary>
    /// The workflow status
    /// </summary>
    internal WorkflowStatus Status { get; set; }
    
    /// <summary>
    /// The payload
    /// </summary>
    internal AnySchemaNode? Payload { get; set; }
    
    /// <summary>
    /// The error
    /// </summary>
    internal Exception? Error { get; set; }
    
    #endregion
    
    #region Abstract

    /// <summary>
    /// Process the workflow
    /// </summary>
    public abstract Task ProcessAsync();

    #endregion
}

/// <summary>
/// The workflow state interface
/// </summary>
public interface IWorkflowState<T>
{
    /// <summary>
    /// The workflow state
    /// </summary>
    T State { get; set; }
}

/// <summary>
/// The workflow session interface
/// </summary>
public interface IWorkflowSession<T>
{
    /// <summary>
    /// The workflow session
    /// </summary>
    T Session { get; set; }
}

public interface IWorkflowPayload
{
    /// <summary>
    /// Sets the payload and done the workflow
    /// </summary>
    public void SetPayload(AnySchemaNode? payload)
    {
        Workflow? workflow = this as Workflow;
        workflow?.Context.Done(workflow, payload);
    }
}

public interface IWorkflowPayload<in T>: IWorkflowPayload
{
    /// <summary>
    /// Sets the payload and done the workflow
    /// </summary>
    public void SetPayload(T? payload)
    {
        if (this is not Workflow workflow) return;
        SetPayload(workflow.Context.GetSchemaTypeAsync(typeof(T).GetSchemaType()!)
            .GetAwaiter().GetResult()!
            .CreateNode(payload));
    }
}
