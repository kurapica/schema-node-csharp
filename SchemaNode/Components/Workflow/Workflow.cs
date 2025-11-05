using SchemaNode.Context;

namespace SchemaNode.Components;

/// <summary>
/// The workflow base class
/// </summary>
public abstract class Workflow
{
    #region Abstract

    /// <summary>
    /// Process the workflow
    /// </summary>
    public virtual async Task ProcessAsync(WorkflowContext context)
    {
        await Task.Yield();
        context.Done(this);
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
    /// <summary>
    /// The workflow session
    /// </summary>
    T Session { get; set; }
}

public interface IWorkflowPayload<T>
{
    /// <summary>
    /// Sets the payload
    /// </summary>
    public void SetPayload(WorkflowContext context, T? payload)
    {
        // TODO
    }
}