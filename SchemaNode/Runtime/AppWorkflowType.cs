using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using SchemaNode.Components.Context;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory application workflow schema representation
/// </summary>
public class AppWorkflowType: IDisposable
{
    #region Properties

    /// <summary>
    /// The application name
    /// </summary>
    public string App => Application.Name;
    
    /// <summary>
    /// The seqNo
    /// </summary>
    public int Seqno { get; internal set; }

    /// <summary>
    /// The workflow name
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// The workflow display name
    /// </summary>
    public LocaleString? Display { get; private set; }
    
    /// <summary>
    /// The workflow description
    /// </summary>
    public LocaleString? Desc { get; private set; }

    /// <summary>
    /// Active the workflow
    /// </summary>
    public bool Active { get; internal set; }
    
    /// <summary>
    /// The workflow nodes
    /// </summary>
    public AppWorkflowNodeSchema[] Nodes { get; internal set; } = [];
    
    /// <summary>
    /// The additional data
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; internal set; }
    
    #endregion
    
    #region States

    public SchemaNodeStatus Status { get;internal set; } = SchemaNodeStatus.Ready;
    
    /// <summary>
    /// Whether the workflow is activated
    /// </summary>
    public bool Activated { get; private set; }

    #endregion

    #region Relationship

    /// <summary>
    /// The application
    /// </summary>
    public AppType Application { get; internal set; } = null!;

    #endregion

    #region Methods

    /// <summary>
    /// Load the workflow schema
    /// </summary>
    public async Task LoadAsync(SchemaContext context)
    {
        // Init the entry workflow context
        if (Nodes.Length <= 1 || !Active) return;
        await ActiveAsync(context);
    }

    /// <summary>
    /// Active the workflow
    /// </summary>
    public async Task ActiveAsync(SchemaContext context)
    {
        if (Activated) return;
        Activated = true;
        
        // init the workflow nodes
        List<Workflow> topNodes = [];
        Dictionary<string, Workflow> workflows = [];

        foreach (var node in Nodes)
        {
            var workflowType = await context.GetSchemaTypeAsync(node.Type) as WorkflowType;
            Type csharpType = workflowType?.ToCSharpType() ?? throw new InvalidOperationException($"Workflow type {node.Type} not found");

            // All constructors parameters goto state, init directly
            Workflow wNode = (Workflow)Activator.CreateInstance(csharpType)!;
            wNode.Application = Application;
            wNode.Name = node.Name;
            wNode.Fork = node.Fork ?? false;
            
            // payload type
            if (!string.IsNullOrWhiteSpace(node.Payload))
            {
                wNode.PayloadType = await context.GetSchemaTypeAsync(node.Payload);
            }

            // state
            if (!string.IsNullOrEmpty(workflowType.State) && node.State != null && !node.State.IsEmpty())
            {
                var stateSchemaType = await context.GetSchemaTypeAsync(workflowType.State);
                var stateType = stateSchemaType?.ToCSharpType();
                if (stateType != null)
                {
                    csharpType.GetProperty("State", BindingFlags.Public | BindingFlags.Instance)
                        ?.SetValue(wNode, stateType.TryConvert(node.State));
                }
            }

            // details
            switch (wNode)
            {
                case FunctionWorkflow funcWorkflow:
                    funcWorkflow.Function = (!string.IsNullOrWhiteSpace(node.Func)
                                                ? await context.GetSchemaTypeAsync(node.Func) as FunctionType
                                                : null)
                                            ?? throw new InvalidOperationException($"Function name is required for function workflow node {node.Name}");
                    funcWorkflow.FuncArgs = node.FuncArgs?.Select(n => new FuncCallArg
                    {
                        Name = n.Name,
                        Value = n.Value,
                    }).ToArray() ?? [];
                    break;

                case EventWorkflow evWorkflow:
                    evWorkflow.Event = (!string.IsNullOrWhiteSpace(node.Event)
                                           ? await context.GetSchemaTypeAsync(node.Event) as EventType
                                           : null)
                                       ?? throw new InvalidOperationException($"Event name is required for event workflow node {node.Name}");
                    break;
            }

            // args
            if (workflowType.Args is { Length: > 0 })
            {
                wNode.Args = new FuncCallArg[workflowType.Args.Length];
                if (node.Args == null || node.Args.Length != workflowType.Args.Length)
                    throw new InvalidOperationException($"Workflow node {node.Name} arguments count mismatch, expected {workflowType.Args.Length} but got {node.Args?.Length ?? 0}");
                for (int i = 0; i < workflowType.Args.Length; i++)
                {
                    var argDef = workflowType.Args[i];
                    var argNode = node.Args[i];
                    wNode.Args[i] = new FuncCallArg
                    {
                        Name = argNode.Name,
                        Value = argNode.Value,
                        TypeNode = await context.GetSchemaTypeAsync(argDef.Type),
                    };
                }
            }
            
            workflows.Add(wNode.Name, wNode);

            // Relations
            if (node.Previous is { Length: > 0 })
            {
                wNode.Previous = new Workflow[node.Previous.Length];
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
        
        // TODO: maybe support multiple entry nodes
        if (topNodes.Count != 1)
            throw new InvalidOperationException($"Workflow schema {Name} should have exactly one entry node, but found {topNodes.Count}");

        _workflowContext?.Dispose();
        _workflowContext = ActivatorUtilities.CreateInstance<WorkflowContext>(context.ServiceProvider);
        
        // restore
        Workflow first = topNodes.First();
        IWorkflowContextPersistence? persistence = context.ServiceProvider.GetService<IWorkflowContextPersistence>();
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
        if (!Activated) return;

        await Task.Yield();

        if (_workflowContext != null)
        {
            await _workflowContext.TerminateAsync();
            _workflowContext = null;
        }
        Activated = false;
    }
    
    public void Dispose()
    {
        _workflowContext?.Dispose();
    }

    private WorkflowContext? _workflowContext;

    #endregion

    #region Conversions

    public static implicit operator AppWorkflowType(AppWorkflowSchema schema)
    {
        return new AppWorkflowType
        {
            Name = schema.Name,
            Seqno = schema.Seqno,
            Display = schema.Display,
            Desc = schema.Desc,
            Active = schema.Active,
            Nodes = schema.Nodes.ToArray(),
            Additional = schema.Additional,
        };
    }

    public static implicit operator AppWorkflowSchema(AppWorkflowType type)
    {
        return new AppWorkflowSchema
        {
            App = type.App,
            Name = type.Name,
            Display = type.Display,
            Desc = type.Desc,
            Seqno = type.Seqno,
            Active = type.Activated,
            Nodes = type.Nodes.ToArray(),
            Additional = type.Additional
        };
    }
    
    #endregion
}