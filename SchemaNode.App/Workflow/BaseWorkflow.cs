using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Schema;
using AppType = SchemaNode.Runtime.AppType;
using ValueType = SchemaNode.Runtime.ValueType;
using static SchemaNode.Utility.AppConstant;
using SchemaType = SchemaNode.Property.Core.SchemaType;

// ReSharper disable CollectionNeverQueried.Global

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable InconsistentNaming

namespace SchemaNode.Workflow;

/// <summary>
/// The workflow base class
/// </summary>
public abstract class BaseWorkflow
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
    internal AppType Application { get; set; } = null!;

    /// <summary>
    /// The workflow name
    /// </summary>
    internal string Name { get; set; } = string.Empty;

    /// <summary>
    /// The previous workflows
    /// </summary>
    internal BaseWorkflow[]? Previous { get; set; }

    /// <summary>
    /// The next workflows
    /// </summary>
    internal BaseWorkflow[]? Next { get; set; }
    
    /// <summary>
    /// The workflow arguments
    /// </summary>
    internal CallArg[]? Args { get; set; }
    
    /// <summary>
    /// The payload type
    /// </summary>
    internal ValueType? PayloadType { get; set; }

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
    internal bool SavePayload { get; set; }

    #endregion

    #region Method

    /// <summary>
    /// Sets the payload, the workflow will be marked as done or fork a new workflow context for the next workflow
    /// </summary>
    protected void SetPayload(WorkflowContext context, object? payload = null, Access? access = null)
        => context.Done(this, payload != null ? PayloadType?.From(payload) : null, access);
    
    /// <summary>
    /// Find the next workflow by name(include self)
    /// </summary>
    internal BaseWorkflow? FindByName(string name)
        => Name.Equals(name, StringComparison.OrdinalIgnoreCase)
            ? this
            : Next?.Select(next => next.FindByName(name)).OfType<BaseWorkflow>().FirstOrDefault();
    
    /// <summary>
    /// Whether it has forks in next nodes
    /// </summary>
    internal bool HasForksInNextNodes => Next != null && Next.Any(n => n.HasForksInNextNodes);
    
    #endregion

    #region Abstract

    /// <summary>
    /// Loading the workflow node with application workflow node schema
    /// </summary>
    public virtual Task LoadAsync(SchemaContext context, AppWorkflowNodeSchema schema) => Task.CompletedTask;

    // Process the workflow
    //public abstract Task ProcessAsync(WorkflowContext context, arg1, arg2, ...);
    //public abstract Task<Session> ProcessAsync<Session>(WorkflowContext context, Session, arg1, arg2, ...);

    #endregion
}

/// <summary>
/// The workflow settings interface
/// </summary>
public interface IWorkflowSettings<T>
{
    /// <summary>
    /// The workflow settings
    /// </summary>
    T? Settings { get; set; }
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
/// The workflow has typed payload interface
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IWorkflowPayload<T>;


/// <summary>
/// Represents the node name in the current workspace
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_WORKFLOW_NODE)]
public class NodeName : Scalar.String;