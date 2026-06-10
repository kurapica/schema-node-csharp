using SchemaNode.Context;
using SchemaNode.Runtime;
using SchemaNode.Schema;
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable InconsistentNaming

namespace SchemaNode.Components;

/// <summary>
/// The workflow base class
/// </summary>
public abstract class Workflow
{
    /// <summary>
    /// The abstract method name for workflow processing
    /// The method signature should be:
    /// @"Task ProcessAsync(WorkflowContext context, arg1, arg2, ...);"
    /// or
    /// @"Task&lt;Session&gt; ProcessAsync&lt;Session&gt;(WorkflowContext context, Session, arg1, arg2, ...);"
    /// </summary>
    public const string WORKFLOW_PROCESS_METHOD = "ProcessAsync";
    
    #region Properties

    /// <summary>
    /// The application owner
    /// </summary>
    internal AppType Application { get; set; } = default!;

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
    /// The workflow arguments
    /// </summary>
    internal FuncCallArg[]? Args { get; set; }
    
    /// <summary>
    /// The payload type
    /// </summary>
    internal NodeType? PayloadType { get; set; }

    /// <summary>
    /// Whether the node can be triggered multiple times
    /// So we need fork the work flow
    /// </summary>
    internal bool Fork { get; set; }

    /// <summary>
    /// Fork primary key of the access path, used to identify different fork instances
    /// If a new workflow comes with the same fork key that not terminated, the new one will be ignored
    /// </summary>
    internal string[]? ForkKey { get; set; }
    
    /// <summary>
    /// Whether the current workflow is un-cancelable
    /// </summary>
    internal bool UnCancelable { get; set; }
    
    /// <summary>
    /// Cancel the previous workflow(s) when this workflow is triggered
    /// </summary>
    internal bool CancelPre { get; set; }
    
    /// <summary>
    /// Notify payload to save in workflow context
    /// </summary>
    internal bool PayloadSave { get; set; }

    #endregion

    #region Method

    /// <summary>
    /// Sets the payload, the workflow will be marked as done or fork a new workflow context for the next workflow
    /// </summary>
    protected void SetPayload(WorkflowContext context, object? payload = null, Access? access = null)
        => context.Done(this, payload != null ? PayloadType?.CreateNode(payload) : null, access);
    
    /// <summary>
    /// Find the next workflow by name(include self)
    /// </summary>
    internal Workflow? FindByName(string name)
        => Name.Equals(name, StringComparison.OrdinalIgnoreCase)
            ? this
            : Next?.Select(next => next.FindByName(name)).OfType<Workflow>().FirstOrDefault();
    
    /// <summary>
    /// Whether it has forks in next nodes
    /// </summary>
    internal bool HasForksInNextNodes => Next != null && Next.Any(n => n.HasForksInNextNodes);
    
    #endregion

    #region Abstract

    // Process the workflow
    //abstract Task ProcessAsync(WorkflowContext context, arg1, arg2, ...);
    //abstract Task<Session> ProcessAsync<Session>(WorkflowContext context, Session, arg1, arg2, ...);
    
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
    T? State { get; set; }
}

/// <summary>
/// The workflow session interface
/// </summary>
public interface IWorkflowSession<in T>
{
    /// <summary>
    /// Release the workflow session
    /// </summary>
    Task ReleaseSessionAsync(WorkflowContext context, T? session);
}

/// <summary>
/// The workflow has payload interface
/// </summary>
public interface IWorkflowPayload
{
}

/// <summary>
/// The workflow has typed payload interface
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IWorkflowPayload<T>: IWorkflowPayload
{
}
