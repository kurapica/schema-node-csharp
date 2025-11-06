using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
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

    /// <summary>
    /// Whether the node can be triggered multiple times
    /// So we need fork the work flow
    /// </summary>
    internal bool Fork { get; set; } = false;

    #endregion

    #region Method

    /// <summary>
    /// Find the next workflow by name(include self)
    /// </summary>
    public Workflow? FindByName(string name)
    {
        if (Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            return this;
        
        if (Next == null || Next.Length == 0) return null;
        foreach (Workflow next in Next)
        {
            Workflow? found = next.FindByName(name);
            if (found != null) return found;
        }
        return null;
    }

    #endregion

    #region Abstract

    /// <summary>
    /// Process the workflow
    /// </summary>
    public abstract Task ProcessAsync(WorkflowContext context);

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
    /// Process with the session
    /// </summary>
    public abstract Task<T> ProcessAsync(WorkflowContext context, T session);
}

public interface IWorkflowPayload
{
    /// <summary>
    /// Sets the payload and done the workflow
    /// </summary>
    public void SetPayload(WorkflowContext context, AnySchemaNode? payload)
    {
        Workflow? workflow = this as Workflow;
        context.Done(workflow, payload);
    }
}

public interface IWorkflowPayload<in T>: IWorkflowPayload
{
    /// <summary>
    /// Sets the payload and done the workflow
    /// </summary>
    public void SetPayload(WorkflowContext context, T? payload)
    {
        if (this is not Workflow workflow) return;
        SetPayload(context, context.GetSchemaTypeAsync(typeof(T).GetSchemaType()!)
            .GetAwaiter().GetResult()!
            .CreateNode(payload));
    }
}
