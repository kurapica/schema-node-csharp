using System.Reactive;
using System.Reflection;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Context;
using SchemaNode.Enum.Workflow;
using SchemaNode.Property.Core;
using SchemaNode.Schema;
using SchemaNode.Utility;
using SchemaNode.Workflow;
using static SchemaNode.Utility.WorkflowConstant;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory workflow representation, the runtime of the node.workflow definition.
/// It builds and activates the workflow node graph, the app layer only references this type.
/// </summary>
public sealed class WorkflowType : NodeType
{
    #region Fields

    private WorkflowSchema? _workflowSchema;
    private NodeType[]? _refTypes;
    private WorkflowStepSchema[] _steps => _workflowSchema?.Steps ?? [];

    #endregion

    #region Property

    /// <summary>
    /// Whether persistence the workflow
    /// </summary>
    public bool Persistence => _workflowSchema?.Persistence ?? false;

    /// <summary>
    /// Whether the workflow context enable full mode
    /// </summary>
    public bool FullMode => _workflowSchema?.FullMode ?? false;

    /// <summary>
    /// The payload type
    /// </summary>
    public ValueType? Payload { get; private set; }

    /// <summary>
    /// The workflow arguments
    /// </summary>
    public FunctionNodeArgument[] Args { get; private set; } = [];

    #endregion

    #region Methods

    /// <inheritdoc />
    public override async Task LoadAsync(SchemaContext context, CancellationToken token = default)
    {
        _workflowSchema = GetProperty<WorkflowProperty>()?.Value;
        if (_workflowSchema == null)
        {
            Error = ErrorCodes.NO_DEFINITION;
            return;
        }

        // Return type
        if (!string.IsNullOrWhiteSpace(_workflowSchema?.Payload))
        {
            ValueType? payloadType = await context.GetNodeTypeAsync<ValueType>(_workflowSchema.Payload, Generics, GenericParams, token: token).ConfigureAwait(false);
            if (payloadType == null || payloadType is GenericType)
            {
                Error ??= WorkflowErrorCodes.WORKFLOW_PAYLOAD_NOT_VALID;
                return;
            }
            Payload = payloadType;
        }

        Args = _workflowSchema?.Args?.Select(a => (FunctionNodeArgument)a).ToArray() ?? [];

        // Argument types
        HashSet<string> existNames = [];
        foreach (FunctionNodeArgument arg in Args)
        {
            if (string.IsNullOrWhiteSpace(arg.Name))
            {
                Error ??= ErrorCodes.FUNC_ARG_NO_NAME;
                return;
            }
            else if (!existNames.Add(arg.Name))
            {
                Error ??= ErrorCodes.FUNC_ARG_DUPLICATE_NAME;
                return;
            }

            arg.ValueType = !string.IsNullOrWhiteSpace(arg.Type) ? await context.GetNodeTypeAsync<ValueType>(arg.Type, Generics, GenericParams, token: token).ConfigureAwait(false) : null;

            if (arg.ValueType == null || arg.ValueType is GenericType)
            {
                Error ??= ErrorCodes.FUNC_ARG_WRONG_TYPE;
                return;
            }
        }

        // _steps
        HashSet<NodeType> refTypes = [];
        foreach (var node in _steps)
        {
            var (subRefTypes, err) = await node.LoadPropertiesAsync(context, node.GetProperties(context.Runtime.GetSchemaKindPropertyTypes(SCHEMA_KIND_NODE_WORKFLOW_STEP)).ToArray(), token: token).ConfigureAwait(false);
            Error ??= err;
            if (subRefTypes is { Length: > 0 })
                foreach (var refType in subRefTypes) refTypes.Add(refType);

            if (string.IsNullOrWhiteSpace(node.Payload)) continue;
            node.PayloadValueType = await context.GetNodeTypeAsync<ValueType>(node.Payload, token: token).ConfigureAwait(false);
            if (node.PayloadValueType == null)
                Error ??= WorkflowErrorCodes.WORKFLOW_NODE_VALUE_TYPE_NOT_VALID;
        }
        _refTypes = refTypes.Count == 0 ? null : refTypes.ToArray();
    }

    public override void Unload()
    {
        Payload = null;
        Args = [];
        _refTypes = null;
    }

    /// <summary>
    /// Gets the reference types
    /// </summary>
    public override IEnumerable<NodeType> GetReferenceTypes()
    {
        if (Payload != null)
            yield return Payload;

        if (Args is { Length: > 0 })
            foreach (var arg in Args)
                if (arg.ValueType is not null)
                    yield return arg.ValueType;

        foreach (var node in _steps)
            if (node.PayloadValueType != null)
                yield return node.PayloadValueType;

        if (_refTypes != null)
            foreach (var node in _refTypes)
                yield return node;
    }

    /// <summary>
    /// Init the workflow context
    /// </summary>
    public Task<WorkflowContext?> ProcessWorkflowAsync(SchemaContext context, Action<WorkflowLog> logHandler, CancellationToken token = default)
        => ProcessWorkflowAsync(context, null, Observer.Create(logHandler), token);

    /// <summary>
    /// Init the workflow context
    /// </summary>
    public Task<WorkflowContext?> ProcessWorkflowAsync(SchemaContext context, object?[] args, Action<WorkflowLog> logHandler, CancellationToken token = default)
        => ProcessWorkflowAsync(context, args, Observer.Create(logHandler), token);

    /// <summary>
    /// Init the workflow context
    /// </summary>
    public async Task<WorkflowContext?> ProcessWorkflowAsync(SchemaContext context, object?[]? args = null, IObserver<WorkflowLog>? logObserver = null, CancellationToken token = default)
    {
        if (Error != null || IsGeneric) return null;

        // init the workflow nodes
        List<BaseWorkflow> topNodes = [];
        Dictionary<string, BaseWorkflow> workflows = [];

        try
        {
            foreach (var node in _steps)
            {
                if (token.IsCancellationRequested) return null;
                var workflowType = await context.GetNodeTypeAsync(node.Type, token: token).ConfigureAwait(false) as WorkflowNodeType;
                Type csharpType = workflowType?.GetCsharpType() ?? throw new InvalidOperationException($"BaseWorkflow type {node.Type} not found");

                // All constructors parameters goto state, init directly
                BaseWorkflow wNode = (BaseWorkflow)Activator.CreateInstance(csharpType)!;
                wNode.Name = node.Name;
                wNode.Fork = node.Fork ?? false;
                wNode.ForkKey = node.ForkKey?.ToArray();
                wNode.UnCancelable = node.UnCancelable ?? false;
                wNode.CancelPre = node.CancelPre ?? false;
                wNode.SavePayload = node.SavePayload ?? false;

                // payload type
                if (node.PayloadValueType != null)
                    wNode.PayloadType = node.PayloadValueType;

                // state
                if (workflowType.State != null && node.State != null && !node.State.IsEmpty())
                {
                    var stateType = workflowType.State?.GetCsharpType();
                    if (stateType != null)
                        csharpType.GetProperty(nameof(WorkflowNodeType.State), BindingFlags.Public | BindingFlags.Instance)
                            ?.SetValue(wNode, stateType.TryConvert(node.State, out var result) ? result : null);
                }

                // details
                await wNode.LoadAsync(context, node, token).ConfigureAwait(false);
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
                            ValueType = await context.GetNodeTypeAsync<ValueType>(argDef.Type, token: token).ConfigureAwait(false),
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

            if (token.IsCancellationRequested) return null;

            var wfContext = ActivatorUtilities.CreateInstance<WorkflowContext>(context.Services);
            wfContext.CopySchemaContextItem(context); // copy context items

            // Check args
            IValueAccess[]? initArgs = null;
            if (Args is { Length: > 0})
            {
                initArgs = new IValueAccess[Args.Length];
                for (int i = 0; i < Args.Length; i++)
                {
                    initArgs[i] = Args[i].ValueType!.Create(wfContext, args?.ElementAtOrDefault(i));
                    if (!initArgs[i].IsValid)
                        throw new InvalidOperationException($"The workflow {Name} can't start with not valid arguments");
                    initArgs[i].SetPropertyValue<Name, string>(Args[i].Name);
                }
            }

            // Restore
            BaseWorkflow first = topNodes.First();
            IWorkflowContextPersistence? persistence = context.GetService<IWorkflowContextPersistence>();
            if (persistence != null)
            {
                var result = await persistence.ListAsync(Name, null, WorkflowStatus.Running).ConfigureAwait(false);
                if (token.IsCancellationRequested) return null;
                if (result.Item2 > 0)
                {
                    // only should have one running context
                    var snapshot = result.Item1.OrderBy(s => s.Id).First();
                    wfContext.Initialize(this, first, null, snapshot, initArgs, logObserver);
                    return wfContext;
                }
            }

            // Init
            wfContext.Initialize(this, first, null, null, initArgs, logObserver);
            return wfContext;
        }
        catch (Exception ex)
        {
            context.LogError(ex, "The init workflow failed");
            throw;
        }
    }

    #endregion
}
