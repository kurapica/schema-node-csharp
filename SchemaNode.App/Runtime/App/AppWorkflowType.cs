using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Property;
using SchemaNode.Schema;
using SchemaNode.Utility;
using SchemaNode.Workflow;
using System.Reflection;
using static SchemaNode.Utility.AppConstant;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory application workflow schema representation
/// </summary>
public sealed class AppWorkflowType: IDisposable
{
    #region Constructors

    internal AppWorkflowType(AppType app, AppWorkflowSchema schema)
    {
        Application = app;
        _appWorkflowSchema = schema;
    }

    #endregion
    
    #region Fields

    private readonly AppWorkflowSchema _appWorkflowSchema;
    private IProperty[]? _props;
    private NodeType[]? _refTypes;
    
    #endregion
    
    #region Properties

    /// <summary>
    /// The application node
    /// </summary>
    public AppType Application { get; }
    
    /// <summary>
    /// The application name
    /// </summary>
    public string App => Application.Name;
    
    /// <summary>
    /// The seqNo
    /// </summary>
    public int Seqno => _appWorkflowSchema.Seqno;

    /// <summary>
    /// The workflow name
    /// </summary>
    public string Name => _appWorkflowSchema.Name;
    
    /// <summary>
    /// Active the workflow
    /// </summary>
    public bool Active { get; internal set; }
    
    /// <summary>
    /// The workflow nodes
    /// </summary>
    public AppWorkflowNodeSchema[] Nodes { get; internal set; } = [];
    
    /// <summary>
    /// The app workflow schema
    /// </summary>
    internal AppWorkflowSchema Schema => _appWorkflowSchema;
    
    #endregion
    
    #region States

    private int _activated;

    /// <summary>
    /// The application field error
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// Whether the workflow is activated
    /// </summary>
    public bool Activated => _activated > 0;

    /// <summary>
    /// Gets the root workflow context
    /// </summary>
    public WorkflowContext? RootWorkflowContext => _workflowContext;
    
    #endregion

    #region Methods

    /// <summary>
    /// Load the workflow schema
    /// </summary>
    public async Task LoadAsync(SchemaContext context)
    {
        _props = _appWorkflowSchema.GetProperties(context.Runtime.GetSchemaKindPropertyTypes(SCHEMA_KIND_APP_WORKFLOW)).ToArray();
        (_refTypes, Error) = await _appWorkflowSchema.LoadPropertiesAsync(context, _props);

        // Resolve payload types for all nodes
        foreach (var node in Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Payload)) continue;
            node.PayloadValueType = await context.GetNodeTypeAsync<ValueType>(node.Payload);
            if (node.PayloadValueType == null)
                Error ??= AppErrorCodes.WORKFLOW_NODE_VALUE_TYPE_NOT_VALID;
        }

        // Init the entry workflow context
        if (Nodes.Length <= 1 || !Active) return;
        await ActiveAsync(context);
    }

    /// <summary>
    /// Gets the reference types
    /// </summary>
    public IEnumerable<NodeType> GetReferenceTypes()
    {
        foreach (var node in Nodes)
        {
            if (node.PayloadValueType != null)
                yield return node.PayloadValueType;
        }
        if (_refTypes != null)
            foreach (var node in _refTypes)
                yield return node;
    }

    /// <summary>
    /// Get the application workflow schema
    /// </summary>
    /// <returns></returns>
    public AppWorkflowSchema GetSchema()
    {
        AppWorkflowSchema schema = new AppWorkflowSchema
        {
            App = _appWorkflowSchema.App,
            Name = _appWorkflowSchema.Name,
            Seqno = _appWorkflowSchema.Seqno,
            Active = _appWorkflowSchema.Active,
            Nodes = _appWorkflowSchema.Nodes.Select(n =>
            {
                AppWorkflowNodeSchema s = new AppWorkflowNodeSchema
                {
                    Name = n.Name,
                    Type = n.Type,
                    Payload = n.Payload,
                    Args = n.Args?.Select(n => new CallArg
                    {
                        Source = n.Source,
                        Value = n.Value,
                    }).ToArray(),
                    Previous = n.Previous,
                    State = n.State,
                    Fork = n.Fork,
                    ForkKey = n.ForkKey,
                    UnCancelable = n.UnCancelable,
                    CancelPre = n.CancelPre,
                    PayloadSave = n.PayloadSave,
                };
                s.CombineProperties(n);
                return s;
            }).ToArray()
        };
        schema.CombineProperties(_appWorkflowSchema);
        return schema;
    }

    /// <summary>
    /// Gets the property with given type
    /// </summary>
    public T? GetProperty<T>() where T : class, IProperty => _props?.OfType<T>().FirstOrDefault();

    /// <summary>
    /// Gets the constraints
    /// </summary>
    public IEnumerable<T> GetProperties<T>() => _props?.OfType<T>() ?? [];
    
    /// <summary>
    /// Gets the property by property name
    /// </summary>
    public IProperty? GetProperty(string propertyName) => _props?.FirstOrDefault(p => p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Active the workflow
    /// </summary>
    public async Task ActiveAsync(SchemaContext context)
    {
        // Active only once
        if (Interlocked.CompareExchange(ref _activated, 1, 0) != 0) return;
        
        // init the workflow nodes
        List<BaseWorkflow> topNodes = [];
        Dictionary<string, BaseWorkflow> workflows = [];

        foreach (var node in Nodes)
        {
            var workflowType = await context.GetNodeTypeAsync(node.Type) as WorkflowType;
            Type csharpType = workflowType?.GetCsharpType() ?? throw new InvalidOperationException($"BaseWorkflow type {node.Type} not found");

            // All constructors parameters goto state, init directly
            BaseWorkflow wNode = (BaseWorkflow)Activator.CreateInstance(csharpType)!;
            wNode.Application = Application;
            wNode.Name = node.Name;
            wNode.Fork = node.Fork ?? false;
            wNode.ForkKey = node.ForkKey?.ToArray();
            wNode.UnCancelable = node.UnCancelable ?? false;
            wNode.CancelPre = node.CancelPre ?? false;
            wNode.PayloadSave = node.PayloadSave ?? false;

            // payload type
            if (node.PayloadValueType != null)
                wNode.PayloadType = node.PayloadValueType;

            // state
            if (workflowType.State != null && node.State != null && !node.State.IsEmpty())
            {
                var stateType = workflowType.State?.GetCsharpType();
                if (stateType != null)
                    csharpType.GetProperty(nameof(WorkflowType.State), BindingFlags.Public | BindingFlags.Instance)
                        ?.SetValue(wNode, stateType.TryConvert(node.State, out var result) ? result : null);
            }

            // details
            await wNode.LoadAsync(context, node);
            Error ??= node.Error;

            // args
            if (workflowType.Args is { Length: > 0 })
            {
                wNode.Args = new CallArg[workflowType.Args.Length];
                if (node.Args == null || node.Args.Length != workflowType.Args.Length)
                    throw new InvalidOperationException($"BaseWorkflow node {node.Name} arguments count mismatch, expected {workflowType.Args.Length} but got {node.Args?.Length ?? 0}");
                for (int i = 0; i < workflowType.Args.Length; i++)
                {
                    var argDef = workflowType.Args[i];
                    var argNode = node.Args[i];
                    wNode.Args[i] = new CallArg
                    {
                        Source = argNode.Source,
                        Value = argNode.Value,
                        ValueType = await context.GetNodeTypeAsync<ValueType>(argDef.Type),
                    };
                }
            }
            
            workflows.Add(wNode.Name, wNode);

            // Relations
            if (node.Previous is { Length: > 0 })
            {
                wNode.Previous = new BaseWorkflow[node.Previous.Length];
                for (int j = 0; j < node.Previous.Length; j++)
                {
                    var prevNode = workflows[node.Previous[j]] 
                                   ?? throw new InvalidOperationException($"Previous workflow node {node.Previous[j]} not found for node {node.Name}");
                    wNode.Previous[j] = prevNode;
                    prevNode.Next ??= [];
                    prevNode.Next = prevNode.Next.Append(wNode).ToArray();
                }
            }
            else
            {
                topNodes.Add(wNode);
            }
        }
        
        // should have only one entry node
        if (topNodes.Count != 1)
            throw new InvalidOperationException($"BaseWorkflow schema {Name} should have exactly one entry node, but found {topNodes.Count}");

        _workflowContext?.Dispose();
        _workflowContext = ActivatorUtilities.CreateInstance<WorkflowContext>(context.Services);
        _workflowContext.CopySchemaContextItem(context); // copy context items
        
        // restore
        BaseWorkflow first = topNodes.First();
        IWorkflowContextPersistence? persistence = context.GetService<IWorkflowContextPersistence>();
        if (persistence != null)
        {
            var result = await persistence.ListAsync(App, Name, null, WorkflowStatus.Running);
            if (result.Item2 > 0)
            {
                // only should have one running context
                var snapshot = result.Item1.OrderBy(s => s.Id).First();
                _workflowContext.Initialize(this, first, null, snapshot);
                return;
            }
        }
        _workflowContext.Initialize(this, first);
    }

    /// <summary>
    /// Deactivate the workflow
    /// </summary>
    public async Task DeactivateAsync()
    {
        if (Interlocked.CompareExchange(ref _activated, 0, 1) != 1)
            return;

        await Task.Yield();

        if (_workflowContext != null)
        {
            await _workflowContext.TerminateAsync();
            _workflowContext = null;
        }
    }
    
    public void Dispose() => _workflowContext?.Dispose();

    private WorkflowContext? _workflowContext;

    #endregion
}