using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Windows.Input;
using SchemaNode.Avalonia.Man.Services;
using SchemaNode.Avalonia.View;
using SchemaNode.Avalonia.View.NodeWrapper;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;

namespace SchemaNode.Avalonia.Man.NodeWrapper;

/// <summary>
/// Function definition card layout for the Man host, mirroring schema-node-man/src/view/func.vue.
///
/// The generic struct view renders return / args / exps as sibling fields. Here they become
/// dedicated cards: return stays a single editor, every argument card carries its own preview
/// input (created from the argument type), and every expression card carries the evaluated
/// result. Other fields (when Core adds any) keep the generic rendering.
///
/// Registration is host-local: this wrapper is only used for <c>system.schema.func.schema</c>.
/// </summary>
public sealed class FunctionDefinitionWrapper : DataNodeWrapper
{
    private readonly List<DataNodeWrapper> _owned = [];
    private readonly SchemaViewRole _childRole;
    private static readonly JsonSerializerOptions ExpJsonOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private int _inputVersion;
    private int _previewVersion;
    private CancellationTokenSource? _previewCts;

    /// <inheritdoc />
    public override string? DefaultTemplateKey => ManSchemaViews.FunctionDefinitionTemplate;

    public FunctionDefinitionWrapper(
        DataNode node,
        SchemaViewRegistry? registry,
        string? fieldName,
        SchemaViewRole role)
        : base(node, registry, fieldName, role)
    {
        FunctionNodeImpl = node as StructNode
            ?? throw new ArgumentException($"Node '{node.Type.Name}' is not a function definition.", nameof(node));

        _childRole = SchemaViewRole.Field;

        foreach ((StructFieldType field, IValueAccess value) in FunctionNodeImpl.GetFields())
        {
            if (value is not DataNode child) continue;

            if (string.Equals(field.Name, "return", StringComparison.OrdinalIgnoreCase))
            {
                ReturnWrapper = Own(NodeWrapperFactory.Create(child, Registry, field.Name, _childRole));
            }
            else if (string.Equals(field.Name, "args", StringComparison.OrdinalIgnoreCase))
            {
                ArgsNode = child as FuncArgsNode;
            }
            else if (string.Equals(field.Name, "exps", StringComparison.OrdinalIgnoreCase))
            {
                ExpsNode = child as ArrayNode;
            }
            else
            {
                Others.Add(Own(NodeWrapperFactory.Create(child, Registry, field.Name, _childRole)));
            }
        }

        if (ArgsNode is not null)
            Track(ArgsNode.Subscribe(_ => RebuildArgumentCards(), immediate: false));
        if (ExpsNode is not null)
            Track(ExpsNode.Subscribe(_ => RebuildExpressionCards(), immediate: false));

        RebuildArgumentCards();
        RebuildExpressionCards();
    }

    /// <summary>The typed function definition node.</summary>
    public StructNode FunctionNodeImpl { get; }

    /// <summary>Fields other than return / args / exps.</summary>
    public BulkObservableCollection<DataNodeWrapper> Others { get; } = new();

    /// <summary>Return value editor.</summary>
    public DataNodeWrapper? ReturnWrapper { get; private set; }

    /// <summary>Argument cards, one per definition argument.</summary>
    public BulkObservableCollection<FunctionArgCard> ArgCards { get; } = new();

    /// <summary>Expression cards, one per definition expression.</summary>
    public BulkObservableCollection<FunctionExpCard> ExpCards { get; } = new();

    /// <summary>Raw argument collection (signature-owned).</summary>
    public FuncArgsNode? ArgsNode { get; }

    /// <summary>Raw expression collection.</summary>
    public ArrayNode? ExpsNode { get; }

    /// <inheritdoc />
    protected override void OnValueChanged() => SchedulePreview();

    /// <inheritdoc />
    protected override void OnForcedReadOnlyChanged(bool value)
    {
        base.OnForcedReadOnlyChanged(value);

        ReturnWrapper?.SetForcedReadOnly(value);
        foreach (DataNodeWrapper other in Others)
            other.SetForcedReadOnly(value);

        foreach (FunctionArgCard card in ArgCards)
            card.ArgWrapper.SetForcedReadOnly(value);
        foreach (FunctionExpCard card in ExpCards)
            card.ExpWrapper.SetForcedReadOnly(value);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _previewCts?.Cancel();

            foreach (FunctionArgCard card in ArgCards)
                card.Dispose();
            foreach (FunctionExpCard card in ExpCards)
                card.Dispose();

            foreach (DataNodeWrapper wrapper in _owned)
                wrapper.Dispose();
            _owned.Clear();
        }

        base.Dispose(disposing);
    }

    private DataNodeWrapper Own(DataNodeWrapper wrapper)
    {
        _owned.Add(wrapper);
        return wrapper;
    }

    private void RebuildArgumentCards()
    {
        if (IsDisposed) return;

        int version = ++_inputVersion;

        foreach (FunctionArgCard card in ArgCards)
            card.Dispose();
        ArgCards.Clear();

        if (ArgsNode is null) return;

        foreach (IValueAccess item in ArgsNode)
        {
            if (item is not DataNode argNode) continue;

            FunctionArgCard card = new(
                argNode,
                NodeWrapperFactory.Create(argNode, Registry, "args", _childRole),
                new RelayCommand(_ => AddArgument(argNode), _ => CanMutate(ArgsNode)),
                new RelayCommand(_ => RemoveArgument(argNode), _ => CanMutate(ArgsNode)));

            ArgCards.Add(card);
            _ = BuildArgumentInputAsync(card, argNode, version);
        }
    }

    private async Task BuildArgumentInputAsync(FunctionArgCard card, DataNode argNode, int version)
    {
        try
        {
            IValueAccess? typeAccess = argNode.GetAccessValue("type");
            if (typeAccess is not IValueTypeAccess valueType) return;

            IValueAccess created = valueType.Create(NodeImpl.Context, null);
            if (created is not DataNode inputNode) return;
            if (IsDisposed || version != _inputVersion) return;

            string? name = argNode.GetAccessValue("name")?.GetValue<string>();
            DataNodeWrapper wrapper = NodeWrapperFactory.Create(
                inputNode, Registry, name, SchemaViewRole.Field);

            card.SetInput(wrapper);
            Track(inputNode.Subscribe(_ => SchedulePreview(), immediate: false));
            SchedulePreview();
        }
        catch (Exception ex)
        {
            card.InputError = ex.Message;
        }

        await Task.CompletedTask;
    }

    private void AddArgument(DataNode element)
    {
        if (ArgsNode is null) return;
        int index = ArgsNode.IndexOf(element);
        ArgsNode.AddRow(index >= 0 ? index + 1 : null);
    }

    private void RemoveArgument(DataNode element)
    {
        if (ArgsNode is null) return;
        DeleteElement(ArgsNode, element);
    }

    private void RebuildExpressionCards()
    {
        if (IsDisposed) return;

        foreach (FunctionExpCard card in ExpCards)
            card.Dispose();
        ExpCards.Clear();

        if (ExpsNode is null) return;

        foreach (IValueAccess item in ExpsNode)
        {
            if (item is not DataNode expNode) continue;

            FunctionExpCard card = new(
                expNode,
                NodeWrapperFactory.Create(expNode, Registry, "exps", _childRole),
                new RelayCommand(_ => AddExpression(expNode), _ => CanMutate(ExpsNode)),
                new RelayCommand(_ => RemoveExpression(expNode), _ => CanMutate(ExpsNode)));

            ExpCards.Add(card);
        }

        SchedulePreview();
    }

    private void AddExpression(DataNode element)
    {
        if (ExpsNode is null) return;
        int index = ExpsNode.IndexOf(element);
        ExpsNode.AddRow(index >= 0 ? index + 1 : null);
    }

    private void RemoveExpression(DataNode element)
    {
        if (ExpsNode is null) return;
        DeleteElement(ExpsNode, element);
    }

    /// <summary>
    /// Removes the element from the array and releases its node. DelRows only removes the array
    /// entry, so the deleted argument/expression would otherwise stay subscribed.
    /// </summary>
    private static void DeleteElement(ArrayNode array, DataNode element)
    {
        int index = array.IndexOf(element);
        if (index < 0) return;

        array.DelRows(index);
        element.Dispose();
    }

    private bool CanMutate(ArrayNode? array)
        => !IsDisposed && array is not null && IsEditable;

    private void SchedulePreview()
    {
        if (IsDisposed || NodeImpl.Context is not SchemaContext) return;

        _previewCts?.Cancel();
        CancellationTokenSource cts = new();
        _previewCts = cts;

        int version = ++_previewVersion;
        _ = EvaluateAfterDelayAsync(version, cts.Token);
    }

    private async Task EvaluateAfterDelayAsync(int version, CancellationToken token)
    {
        try
        {
            await Task.Delay(500, token);
            await EvaluateAsync(version, token);
        }
        catch (OperationCanceledException)
        {
            // Typing or a signature change superseded this evaluation.
        }
    }

    private async Task EvaluateAsync(int version, CancellationToken token)
    {
        if (NodeImpl.Context is not SchemaContext context) return;

        Dictionary<string, JsonNode?> datas = [];
        foreach (FunctionArgCard card in ArgCards)
        {
            string? name = card.ArgName;
            if (string.IsNullOrWhiteSpace(name)) continue;
            datas[name] = ToJson(card.Input?.Node.GetValue<object>());
        }

        foreach (FunctionExpCard card in ExpCards)
        {
            if (IsDisposed || version != _previewVersion || token.IsCancellationRequested) return;

            FuncExp? exp = ReadExpression(card.ExpNode);
            if (exp?.Call is null || string.IsNullOrWhiteSpace(exp.Call.Func))
            {
                card.Result = string.Empty;
                continue;
            }

            try
            {
                JsonArray args = [];
                foreach (CallArg arg in exp.Call.Args)
                {
                    args.Add(arg.Source is null
                        ? arg.Value?.DeepClone()
                        : ResolveSource(arg.Source, datas));
                }

                JsonNode? result = await context.CallFunctionAsync(
                    exp.Call.Func, args, exp.Return, exp.Call.Mode);

                if (IsDisposed || version != _previewVersion) return;
                datas[exp.Name] = result?.DeepClone();
                card.Result = result?.ToJsonString() ?? "null";
            }
            catch (Exception ex)
            {
                if (IsDisposed || version != _previewVersion) return;
                card.Result = ex.Message;
            }
        }
    }

    private static FuncExp? ReadExpression(DataNode expNode)
    {
        object? raw = expNode.GetValue<object>();
        if (raw is FuncExp exp) return exp;
        if (raw is not JsonNode json) return null;

        try
        {
            return JsonSerializer.Deserialize<FuncExp>(json.ToJsonString(), ExpJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonNode? ResolveSource(string source, Dictionary<string, JsonNode?> datas)
    {
        string[] paths = source.Split('.', StringSplitOptions.RemoveEmptyEntries);
        JsonNode? current = datas.GetValueOrDefault(paths[0]);
        for (int i = 1; i < paths.Length && current is not null; i++)
        {
            if (current is JsonObject obj)
            {
                current = obj.TryGetPropertyValue(paths[i], out JsonNode? child) ? child : null;
            }
            else if (current is JsonArray array && int.TryParse(paths[i], out int index) && index < array.Count)
            {
                current = array[index];
            }
            else
            {
                current = null;
            }
        }

        return current?.DeepClone();
    }

    private static JsonNode? ToJson(object? raw)
        => raw is null ? null : raw is JsonNode node ? node.DeepClone() : JsonSerializer.SerializeToNode(raw);
}

/// <summary>One argument card: the definition field, its operations, and the preview input.</summary>
public sealed class FunctionArgCard : INotifyPropertyChanged
{
    private DataNodeWrapper? _input;
    private string? _inputError;

    internal FunctionArgCard(DataNode argNode, DataNodeWrapper argWrapper, ICommand add, ICommand remove)
    {
        ArgNode = argNode;
        ArgWrapper = argWrapper;
        AddCommand = add;
        RemoveCommand = remove;
    }

    /// <summary>Argument definition node.</summary>
    public DataNode ArgNode { get; }

    /// <summary>Argument definition editor.</summary>
    public DataNodeWrapper ArgWrapper { get; }

    /// <summary>Insert another argument after this one.</summary>
    public ICommand AddCommand { get; }

    /// <summary>Delete this argument.</summary>
    public ICommand RemoveCommand { get; }

    /// <summary>Argument name shown on the card.</summary>
    public string? ArgName => ArgNode.GetAccessValue("name")?.GetValue<string>();

    /// <summary>Preview input created from the argument type.</summary>
    public DataNodeWrapper? Input => _input;

    /// <summary>Whether a preview input could be created.</summary>
    public bool HasInput => _input is not null;

    /// <summary>Reason why no input could be created.</summary>
    public string? InputError
    {
        get => _inputError;
        set
        {
            if (_inputError == value) return;
            _inputError = value;
            Raise(nameof(InputError));
            Raise(nameof(HasInput));
        }
    }

    internal void SetInput(DataNodeWrapper wrapper)
    {
        _input = wrapper;
        _inputError = null;
        Raise(nameof(Input));
        Raise(nameof(HasInput));
        Raise(nameof(InputError));
    }

    internal void Dispose() => _input?.Dispose();

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise(string name)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>One expression card: the expression editor plus its evaluated result.</summary>
public sealed class FunctionExpCard : INotifyPropertyChanged
{
    private string _result = string.Empty;

    internal FunctionExpCard(DataNode expNode, DataNodeWrapper expWrapper, ICommand add, ICommand remove)
    {
        ExpNode = expNode;
        ExpWrapper = expWrapper;
        AddCommand = add;
        RemoveCommand = remove;
    }

    /// <summary>Expression node.</summary>
    public DataNode ExpNode { get; }

    /// <summary>Expression editor.</summary>
    public DataNodeWrapper ExpWrapper { get; }

    /// <summary>Insert another expression after this one.</summary>
    public ICommand AddCommand { get; }

    /// <summary>Delete this expression.</summary>
    public ICommand RemoveCommand { get; }

    /// <summary>Expression name shown on the card.</summary>
    public string? ExpName => ExpNode.GetAccessValue("name")?.GetValue<string>();

    /// <summary>Evaluated result (JSON or the failure message).</summary>
    public string Result
    {
        get => _result;
        set
        {
            if (_result == value) return;
            _result = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Result)));
        }
    }

    internal void Dispose() => ExpWrapper.Dispose();

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;
}
