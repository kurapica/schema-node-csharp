using SchemaNode.Context;
using SchemaNode.Runtime;
using SchemaNode.Schema;
// ReSharper disable SuspiciousTypeConversion.Global

namespace SchemaNode.Components;

/// <summary>
/// The workflow base class
/// </summary>
public abstract class Workflow
{
    #region Properties

    /// <summary>
    /// The application
    /// </summary>
    public AppType Application { get; internal set; } = default!;

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
    /// The function call arguments
    /// </summary>
    internal FuncCallArg[] Args { get; set; } = [];
    
    /// <summary>
    /// The payload type
    /// </summary>
    internal AnySchemeType? PayloadType { get; set; }

    /// <summary>
    /// Whether the node can be triggered multiple times
    /// So we need fork the work flow
    /// </summary>
    internal bool Fork { get; set; } = false;

    #endregion

    #region Method

    /// <summary>
    /// Sets the payload and done the workflow
    /// </summary>
    protected void SetPayload(WorkflowContext context, object? payload)
    {
        context.Done(this, payload != null 
            ? PayloadType?.CreateNode(payload)
            : null);
    }
    
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
    public virtual Task ProcessAsync(WorkflowContext context)
    {
        throw new NotImplementedException($"The workflow '{Name}' does not implement the ProcessAsync method.");
    }

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
    /// Process with the session
    /// <summary>
    /// </summary>
    public virtual Task<T> ProcessAsync(WorkflowContext context, T? session)
    {
        throw new NotImplementedException($"The workflow session does not implement the ProcessAsync method.");
    }
}

public interface IWorkflowPayload
{
}

public interface IWorkflowPayload<in T>: IWorkflowPayload
{
}
