using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Property.Function;
using SchemaNode.Runtime;
using SchemaNode.WPF.View;
using SchemaNode.WPF.View.Localization;
using SchemaNode.WPF.View.NodeWrapper;
using ValueType = SchemaNode.Runtime.ValueType;

namespace SchemaNode.WPF.Man.Views;

/// <summary>
/// Card layout for the function definition (counterpart of func.vue):
/// return value, argument cards with demo inputs, and expression cards with a live result.
///
/// The view is registered only by the Man registry, structurally for function definition structs
/// (Core merges the function fields into the node schema, so the type name alone is not enough).
/// It reuses the generic <see cref="StructWrapper"/> (no type test in a wrapper); only the layout,
/// the demo inputs and the expression evaluation live here.
///
/// Expression previews are evaluated with Core's schema call API against the demo inputs, so the
/// values follow the argument and expression edits instead of requiring a manual test run.
/// </summary>
public partial class FunctionSchemaView : UserControl, INotifyPropertyChanged
{
    private readonly List<IDisposable> _subscriptions = [];
    private StructWrapper? _wrapper;
    private FunctionNode? _functionNode;
    private int _generation;
    private int _calcVersion;
    private bool _disposed;

    public FunctionSchemaView()
    {
        AddArgumentCommand = new RelayCommand(AddArgument, () => IsEditable);
        AddExpressionCommand = new RelayCommand(AddExpression, () => IsEditable);

        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Add a trailing argument (header button).</summary>
    public ICommand AddArgumentCommand { get; }

    /// <summary>Add a trailing expression (header button).</summary>
    public ICommand AddExpressionCommand { get; }

    /// <summary>The return value field, rendered by the regular view resolution.</summary>
    public DataNodeWrapper? ReturnWrapper { get; private set; }

    /// <summary>Argument cards: definition fields plus a demo input node.</summary>
    public ObservableCollection<FunctionArgCard> ArgCards { get; } = new();

    /// <summary>Expression cards with their live preview.</summary>
    public ObservableCollection<FunctionExpCard> ExpCards { get; } = new();

    /// <summary>Any remaining visible fields of the function struct.</summary>
    public BulkObservableCollection<DataNodeWrapper> Others { get; } = new();

    private bool IsEditable => !_disposed && _wrapper?.IsEditable == true;

    #region Attach / detach

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        Attach(e.NewValue as StructWrapper);
    }

    private void Attach(StructWrapper? wrapper)
    {
        if (_wrapper == wrapper) return;
        Detach();
        _disposed = false;
        _wrapper = wrapper;
        _functionNode = wrapper?.Node as FunctionNode;

        ReturnWrapper = _wrapper is null || _functionNode?.ReturnField is null
            ? null
            : _wrapper.Children.FirstOrDefault(child => child.Node == _functionNode.ReturnField);
        Raise(nameof(ReturnWrapper));
        RaiseCommandStates();

        if (_wrapper is null || _functionNode is null) return;

        foreach (DataNodeWrapper child in _wrapper.Children)
        {
            if (child.Node == _functionNode.ReturnField
                || child.Node == _functionNode.ArgsField
                || child.Node == _functionNode.ExpsField)
                continue;
            Others.Add(child);
        }

        _subscriptions.Add(_functionNode.Subscribe(_ => OnFunctionChanged()));
        if (_functionNode.ArgsField is { } args)
            _subscriptions.Add(args.Subscribe(_ => SyncCards(force: false)));
        if (_functionNode.ExpsField is { } exps)
            _subscriptions.Add(exps.Subscribe(_ => SyncCards(force: false)));

        SyncCards(force: true);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Unloaded clears the cards; when the same view instance is put back (tab switch),
        // DataContextChanged does not fire again, so re-attach explicitly.
        if (_wrapper is null)
            Attach(DataContext as StructWrapper);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Detach();

    private void Detach()
    {
        _disposed = true;
        ++_generation;
        ++_calcVersion;

        foreach (IDisposable subscription in _subscriptions)
            subscription.Dispose();
        _subscriptions.Clear();

        foreach (FunctionArgCard card in ArgCards)
            card.Dispose();
        ArgCards.Clear();
        foreach (FunctionExpCard card in ExpCards)
            card.Dispose();
        ExpCards.Clear();
        Others.ReplaceAll([]);

        ReturnWrapper = null;
        Raise(nameof(ReturnWrapper));
        _wrapper = null;
        _functionNode = null;
        RaiseCommandStates();
    }

    private void RaiseCommandStates()
    {
        if (AddArgumentCommand is RelayCommand addArgument)
            addArgument.RaiseCanExecuteChanged();
        if (AddExpressionCommand is RelayCommand addExpression)
            addExpression.RaiseCanExecuteChanged();
    }

    private void OnFunctionChanged()
    {
        if (_disposed) return;
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(OnFunctionChanged);
            return;
        }

        // Typing in a field must not rebuild the demo nodes; only a shape change (argument type
        // or variadic flag) does. Renaming an argument only updates the data key used by the
        // expression preview, so the card (and its focused editor) stays in place.
        string shape = BuildShape();
        if (shape != _shape)
        {
            SyncCards(force: true);
            return;
        }
        UpdateArgumentNames();
        CalculateSoon();
    }

    #endregion

    #region Cards

    private string _shape = string.Empty;

    private void SyncCards(bool force)
    {
        if (_disposed) return;
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(() => SyncCards(force));
            return;
        }

        List<DataNode> argNodes = ReadElements(_functionNode?.ArgsField);
        List<DataNode> expNodes = ReadElements(_functionNode?.ExpsField);

        bool argsChanged = force || ArgCards.Count != argNodes.Count
                           || ArgCards.Select((card, index) => card.Arg.Node == argNodes[index]).Any(same => !same);
        bool expsChanged = force || ExpCards.Count != expNodes.Count
                           || ExpCards.Select((card, index) => card.Exp.Node == expNodes[index]).Any(same => !same);

        if (argsChanged)
        {
            ++_generation;
            int generation = _generation;

            foreach (FunctionArgCard card in ArgCards)
                card.Dispose();
            ArgCards.Clear();

            foreach (DataNode element in argNodes)
            {
                if (element is not FuncArgNode arg) continue;
                DataNodeWrapper child = NodeWrapperFactory.Create(element, _wrapper!.Registry, "args", _wrapper.Role);
                FunctionArgCard card = new(
                    child,
                    arg.GetAccessValue("name")?.GetValue<string>() ?? string.Empty,
                    arg.GetAccessValue("type")?.GetValue<string>() ?? string.Empty,
                    arg.GetPropertyValue<Variadic, bool>(),
                    new RelayCommand(() => AddArgumentAfter(element), () => IsEditable),
                    new RelayCommand(() => RemoveArgument(element), () => IsEditable));
                ArgCards.Add(card);
                _ = BuildDemoNodeAsync(card, generation);
            }
        }

        if (expsChanged)
        {
            foreach (FunctionExpCard card in ExpCards)
                card.Dispose();
            ExpCards.Clear();
            foreach (DataNode element in expNodes)
            {
                DataNodeWrapper child = NodeWrapperFactory.Create(element, _wrapper!.Registry, "exps", _wrapper.Role);
                ExpCards.Add(new FunctionExpCard(
                    child,
                    new RelayCommand(() => AddExpressionAfter(element), () => IsEditable),
                    new RelayCommand(() => RemoveExpression(element), () => IsEditable)));
            }
        }

        _shape = BuildShape();
        CalculateSoon();
    }

    private static List<DataNode> ReadElements(ArrayNode? node)
        => node is null ? [] : node.OfType<DataNode>().ToList();

    private void AddArgument()
    {
        if (!IsEditable) return;
        _functionNode?.ArgsField?.AddRow();
    }

    private void AddArgumentAfter(DataNode element)
    {
        if (!IsEditable) return;
        ArrayNode? args = _functionNode?.ArgsField;
        if (args is null) return;

        int index = args.IndexOf(element);
        args.AddRow(index >= 0 ? index + 1 : null);
    }

    private void RemoveArgument(DataNode element)
    {
        if (!IsEditable) return;
        ArrayNode? args = _functionNode?.ArgsField;
        if (args is null) return;

        DeleteElement(args, element);
    }

    private void AddExpression()
    {
        if (!IsEditable) return;
        _functionNode?.ExpsField?.AddRow();
    }

    private void AddExpressionAfter(DataNode element)
    {
        if (!IsEditable) return;
        ArrayNode? exps = _functionNode?.ExpsField;
        if (exps is null) return;

        int index = exps.IndexOf(element);
        exps.AddRow(index >= 0 ? index + 1 : null);
    }

    private void RemoveExpression(DataNode element)
    {
        if (!IsEditable) return;
        ArrayNode? exps = _functionNode?.ExpsField;
        if (exps is null) return;

        DeleteElement(exps, element);
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

    private string BuildShape()
    {
        if (_functionNode?.ArgsField is not { } args) return string.Empty;
        return string.Join("|", args.OfType<FuncArgNode>().Select(arg =>
        {
            string? type = arg.GetAccessValue("type")?.GetValue<string>();
            bool variadic = arg.GetPropertyValue<Variadic, bool>();
            return $"{type}:{variadic}";
        }));
    }

    private void UpdateArgumentNames()
    {
        if (_functionNode?.ArgsField is not { } args) return;
        List<FuncArgNode> nodes = args.OfType<FuncArgNode>().ToList();
        for (int i = 0; i < nodes.Count && i < ArgCards.Count; i++)
            ArgCards[i].Name = nodes[i].GetAccessValue("name")?.GetValue<string>() ?? string.Empty;
    }

    private async Task BuildDemoNodeAsync(FunctionArgCard card, int generation)
    {
        if (_functionNode?.Context is not SchemaContext context || string.IsNullOrWhiteSpace(card.TypeName))
            return;

        try
        {
            IValueTypeAccess? type = await context.GetNodeTypeAsync<ValueType>(card.TypeName);
            if (type is null) return;
            if (card.IsVariadic)
                type = await context.GetArrayNodeTypeAsync(type);
            if (type is null) return;

            if (type.Create(context) is not DataNode node)
                return;

            if (_disposed || generation != _generation)
            {
                node.Dispose();
                return;
            }

            card.SetDemoNode(node, node.Subscribe(_ => CalculateSoon()));
            CalculateSoon();
        }
        catch
        {
            // A demo input is a convenience: an unresolvable arg type must not break the editor.
        }
    }

    #endregion

    #region Expression preview

    private async void CalculateSoon()
    {
        if (_disposed || _functionNode is null)
            return;
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(CalculateSoon);
            return;
        }

        int version = ++_calcVersion;
        int generation = _generation;
        try
        {
            await Task.Delay(250);
        }
        catch (Exception)
        {
            return;
        }

        if (_disposed || version != _calcVersion || generation != _generation)
            return;

        await CalculateAsync(generation);
    }

    private async Task CalculateAsync(int generation)
    {
        if (_functionNode?.Context is not SchemaContext context)
            return;

        JsonObject data = new();
        foreach (FunctionArgCard card in ArgCards.ToList())
        {
            JsonNode? value = null;
            card.DemoNode?.TryGetValue(out value);
            data[card.Name] = value?.DeepClone();
        }

        foreach (FunctionExpCard card in ExpCards.ToList())
        {
            if (_disposed || generation != _generation)
                return;

            if (card.Exp.Node is not StructNode expression)
                continue;

            string? name = expression.GetAccessValue("name")?.GetValue<string>();
            string? returnType = expression.GetAccessValue("return")?.GetValue<string>();
            FuncCallNode? call = expression.GetAccessValue("call") as FuncCallNode;
            string? functionName = call?.FuncField?.GetValue<string>();
            if (call is null || string.IsNullOrWhiteSpace(functionName))
            {
                card.SetPreview(LocaleManager.Instance.Get("wpf.man.function.nofunction"));
                continue;
            }

            JsonArray args = new();
            if (call.ArgsField is not null)
            {
                foreach (IValueAccess item in call.ArgsField)
                {
                    if (item is not StructNode argument) continue;
                    string? source = argument.GetAccessValue("source")?.GetValue<string>();
                    JsonNode? value = argument.GetAccessValue("value")?.GetValue<JsonNode>();
                    JsonNode? argumentValue = string.IsNullOrWhiteSpace(source)
                        ? value
                        : Resolve(data, source);
                    args.Add(argumentValue?.DeepClone());
                }
            }

            try
            {
                JsonNode? result = await context.CallFunctionAsync<JsonNode>(
                    functionName, args.Select(node => (object?)node).ToArray(), returnType,
                    call.ModeField?.GetValue<ApplyMode>() ?? ApplyMode.Call);

                if (_disposed || generation != _generation) return;
                string preview = result?.ToJsonString() ?? "null";
                await Dispatcher.InvokeAsync(() => card.SetPreview(preview));
                if (!string.IsNullOrWhiteSpace(name))
                    data[name] = result?.DeepClone();
            }
            catch (Exception error)
            {
                string message = error.Message;
                await Dispatcher.InvokeAsync(() => card.SetPreview(message));
            }
        }
    }

    /// <summary>
    /// Resolves an access path (for example <c>a.field</c> or <c>prev.result</c>) against the
    /// current demo values, mirroring how the vue view resolves call argument sources.
    /// </summary>
    private static JsonNode? Resolve(JsonObject data, string source)
    {
        JsonNode? current = null;
        foreach (string part in source.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current is null)
            {
                if (!data.TryGetPropertyValue(part, out current))
                    return null;
            }
            else if (current is JsonObject obj)
            {
                if (!obj.TryGetPropertyValue(part, out current))
                    return null;
            }
            else if (current is JsonArray array)
            {
                string indexText = part.Trim('[', ']');
                if (!int.TryParse(indexText, out int index) || index < 0 || index >= array.Count)
                    return null;
                current = array[index];
            }
            else
            {
                return null;
            }
        }
        return current;
    }

    #endregion

    private void Raise(params string[] names)
    {
        foreach (string name in names)
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>A definition argument card: editable argument fields plus a demo input node.</summary>
public sealed class FunctionArgCard : INotifyPropertyChanged, IDisposable
{
    private string _name = string.Empty;
    private DataNode? _demoNode;
    private IDisposable? _demoSubscription;

    public FunctionArgCard(DataNodeWrapper arg, string name, string typeName, bool isVariadic,
                           ICommand addCommand, ICommand removeCommand)
    {
        Arg = arg;
        Name = name;
        TypeName = typeName;
        IsVariadic = isVariadic;
        AddCommand = addCommand;
        RemoveCommand = removeCommand;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The argument struct wrapper (name / type / require).</summary>
    public DataNodeWrapper Arg { get; }

    /// <summary>Argument name, used as the data key for expression sources.</summary>
    public string Name
    {
        get => _name;
        set
        {
            if (_name == value) return;
            _name = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }

    /// <summary>Declared argument type, used to build the demo input.</summary>
    public string TypeName { get; }

    /// <summary>Whether the argument is variadic (demo input is a list).</summary>
    public bool IsVariadic { get; }

    /// <summary>Insert a copy position after this argument.</summary>
    public ICommand AddCommand { get; }

    /// <summary>Remove this argument.</summary>
    public ICommand RemoveCommand { get; }

    /// <summary>Demo input node created from the argument type.</summary>
    public DataNode? DemoNode => _demoNode;

    /// <summary>Publishes a freshly resolved demo node.</summary>
    public void SetDemoNode(DataNode node, IDisposable subscription)
    {
        _demoNode = node;
        _demoSubscription = subscription;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DemoNode)));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _demoSubscription?.Dispose();
        _demoSubscription = null;
        _demoNode?.Dispose();
        _demoNode = null;
        Arg.Dispose();
    }
}

/// <summary>An expression card with its evaluated result.</summary>
public sealed class FunctionExpCard : INotifyPropertyChanged, IDisposable
{
    private string _preview = string.Empty;

    public FunctionExpCard(DataNodeWrapper exp, ICommand addCommand, ICommand removeCommand)
    {
        Exp = exp;
        AddCommand = addCommand;
        RemoveCommand = removeCommand;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The expression struct wrapper.</summary>
    public DataNodeWrapper Exp { get; }

    /// <summary>Insert a copy position after this expression.</summary>
    public ICommand AddCommand { get; }

    /// <summary>Remove this expression.</summary>
    public ICommand RemoveCommand { get; }

    /// <summary>The latest preview text (JSON or an error message).</summary>
    public string Preview => _preview;

    /// <summary>Replaces the preview text.</summary>
    public void SetPreview(string text)
    {
        if (_preview == text) return;
        _preview = text;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Preview)));
    }

    /// <inheritdoc />
    public void Dispose() => Exp.Dispose();
}
