using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Input;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using Prism.Navigation.Regions;
using SchemaNode.Api.Client;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.WPF.Man.Services;
using SchemaNode.WPF.View;
using SchemaNode.WPF.View.Localization;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.WPF.Man.ViewModels;

/// <summary>
/// Function definition view model (screen-level).
///
/// The screen layout mirrors func.vue in schema-node-man:
///   - The left side directly renders the property area of the "definition node"
///     (system.schema.node.schema). Return / Args / Exps under the <c>function</c>
///     property are real Core fields; the orchestration of arguments and expressions
///     (including candidate entries for CallArg.Source) is provided by Core's Relation;
///   - The test area on the right creates input nodes from the parameter types
///     resolved by <see cref="FunctionType"/>; calls go through
///     <see cref="ISchemaApiProvider.CallFunctionAsync"/>, and the result is rendered
///     according to the return type.
///
/// Lifecycle: navigation activation creates an isolated session (CreateSession → FullMode
/// is already set by the factory). On leave, the test area Wrapper/DataNode is released
/// first, then the editor Wrapper/DataNode, and finally the Context is destroyed.
/// </summary>
public class FunctionDefinitionViewModel : BindableBase, INavigationAware, IDestructible
{
    private readonly SchemaContextFactory _factory;
    private SchemaScreenSession? _session;

    private string _namespace = "system";
    private string _functionName = string.Empty;
    private NodeSchema? _selectedFunction;
    private string? _status;
    private bool _suppressSelection;

    private FunctionType? _functionType;
    private IValueAccess? _resultNode;
    private string? _resultText;

    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    public FunctionDefinitionViewModel(SchemaContextFactory factory, SchemaEditorViewModel editor)
    {
        _factory = factory;
        Editor = editor;

        LoadListCommand = new DelegateCommand(ExecuteLoadList);
        NewCommand = new DelegateCommand(ExecuteNew);
        SaveCommand = new DelegateCommand(ExecuteSave);
        CallCommand = new DelegateCommand(ExecuteCall);
    }

    /// <summary>Embedded generic definition editor (SchemaView wraps its node when loaded)</summary>
    public SchemaEditorViewModel Editor { get; }

    /// <summary>Function list under the current namespace</summary>
    public ObservableCollection<NodeSchema> Functions { get; } = new();

    /// <summary>Argument input rows in the test area</summary>
    public ObservableCollection<FunctionArgInput> ArgInputs { get; } = new();

    /// <summary>Namespace</summary>
    public string Namespace
    {
        get => _namespace;
        set => SetProperty(ref _namespace, value);
    }

    /// <summary>Full name of the current function (used for save/call)</summary>
    public string FunctionName
    {
        get => _functionName;
        set => SetProperty(ref _functionName, value);
    }

    /// <summary>Selected function (loaded into the left definition area and right test area on switch)</summary>
    public NodeSchema? SelectedFunction
    {
        get => _selectedFunction;
        set
        {
            if (!SetProperty(ref _selectedFunction, value)) return;
            if (_suppressSelection || value?.Name is null || _session is null) return;
            _ = LoadFunctionAsync(value.FullName);
        }
    }

    /// <summary>Operation status message</summary>
    public string? Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    /// <summary>Result value node (wrapped by the view when it renders)</summary>
    public IValueAccess? ResultNode
    {
        get => _resultNode;
        private set
        {
            if (SetProperty(ref _resultNode, value))
                RaisePropertyChanged(nameof(HasResult));
        }
    }

    /// <summary>Whether there is a structured result to render</summary>
    public bool HasResult => _resultNode is not null;

    /// <summary>Raw result JSON (also shown as output when there is no structured node)</summary>
    public string? ResultText
    {
        get => _resultText;
        private set
        {
            if (SetProperty(ref _resultText, value))
                RaisePropertyChanged(nameof(HasResultText));
        }
    }

    /// <summary>Whether there is a raw JSON result</summary>
    public bool HasResultText => !string.IsNullOrEmpty(_resultText);

    public ICommand LoadListCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CallCommand { get; }

    #region Navigation

    public bool IsNavigationTarget(NavigationContext navigationContext) => false;

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        Teardown();
        _session = _factory.CreateSession();
        Editor.Attach(_session);
        Status = LocaleManager.Instance.Get("wpf.function.status.ready");
        ExecuteLoadList();
    }

    public void OnNavigatedFrom(NavigationContext navigationContext) => Teardown();

    public void Destroy() => Teardown();

    private void Teardown()
    {
        // Reverse order: test area Wrapper/DataNode → editor Wrapper/DataNode → Context/Scope
        ClearTestArea();
        Editor.Dispose();
        _session?.Dispose();
        _session = null;
        _functionType = null;
        FunctionName = string.Empty;

        _suppressSelection = true;
        Functions.Clear();
        SelectedFunction = null;
        _suppressSelection = false;
    }

    #endregion

    #region Load / new / save

    private async void ExecuteLoadList()
    {
        if (_session is null || string.IsNullOrWhiteSpace(Namespace)) return;

        try
        {
            // The schema API returns the namespace definition with its direct children nested;
            // this screen lists the function children of the current namespace.
            NodeSchema[] schemas = await _session.Api.GetSchemaAsync([Namespace]);
            NodeSchema[] children = SchemaTreeSearch.Find(schemas, Namespace)?.Schemas ?? [];
            _suppressSelection = true;
            Functions.Clear();
            foreach (NodeSchema schema in children)
                if (schema.Kind.Equals(NODE_KIND_FUNCTION, StringComparison.OrdinalIgnoreCase))
                    Functions.Add(schema);

            NodeSchema? first = Functions.FirstOrDefault();
            SelectedFunction = first;
            _suppressSelection = false;

            Status = Functions.Count == 0
                ? LocaleManager.Instance.Format("wpf.function.status.empty", Namespace)
                : LocaleManager.Instance.Format("wpf.function.status.loaded", Functions.Count);

            if (first is not null)
                await LoadFunctionAsync(first.FullName);
        }
        catch (Exception ex)
        {
            _suppressSelection = false;
            Status = LocaleManager.Instance.Format("wpf.function.status.loadfailed", ex.Message);
        }
    }

    private async Task LoadFunctionAsync(string fullName)
    {
        if (_session is null) return;

        ClearTestArea();
        FunctionName = fullName;

        await Editor.ReloadSchemaAsync(fullName);
        if (Editor.Node is null)
        {
            Status = LocaleManager.Instance.Format("wpf.function.status.loaddef", fullName);
            return;
        }

        await RebuildTestArgsAsync(fullName, reload: false);
        Status = LocaleManager.Instance.Format("wpf.function.status.def.loaded", fullName);
    }

    private async void ExecuteNew()
    {
        if (_session is null) return;

        ClearTestArea();
        _functionType = null;
        Status = LocaleManager.Instance.Get("wpf.function.status.new");

        // kind is a real field of the definition node; set kind=function directly so it
        // does not have to be switched manually after creation.
        // Take only the last segment of the name so the user does not type the namespace
        // into the name box.
        string? simpleName = string.IsNullOrWhiteSpace(FunctionName)
            ? null
            : FunctionName.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();

        await Editor.NewSchemaAsync(new NodeSchema
        {
            Namespace = Namespace,
            Name = simpleName!,
            Kind = NODE_KIND_FUNCTION,
        });
    }

    private async void ExecuteSave()
    {
        if (_session is null) return;

        NodeSchema? schema = Editor.GetNodeSchema();
        if (schema is null)
        {
            Status = LocaleManager.Instance.Get("wpf.function.status.noschema");
            return;
        }

        bool ok = await _session.Api.SaveSchemaAsync(schema);
        if (!ok)
        {
            Status = LocaleManager.Instance.Get("wpf.function.status.savefailed");
            return;
        }

        string fullName = schema.FullName;
        FunctionName = fullName;

        // Saving sets the type Loaded=false; this screen must invalidate and rebuild it
        // before refreshing the test area.
        await Editor.ReloadSchemaAsync(fullName, reload: true);
        await RebuildTestArgsAsync(fullName, reload: true);
        await RefreshListAsync();
        Status = LocaleManager.Instance.Format("wpf.function.status.saved", fullName);
    }

    private async Task RefreshListAsync()
    {
        if (_session is null || string.IsNullOrWhiteSpace(Namespace)) return;

        NodeSchema[] schemas = await _session.Api.GetSchemaAsync([Namespace]);
        NodeSchema[] children = SchemaTreeSearch.Find(schemas, Namespace)?.Schemas ?? [];
        _suppressSelection = true;
        Functions.Clear();
        foreach (NodeSchema schema in children)
            if (schema.Kind.Equals(NODE_KIND_FUNCTION, StringComparison.OrdinalIgnoreCase))
                Functions.Add(schema);
        _suppressSelection = false;
    }

    #endregion

    #region Test area

    /// <summary>
    /// Creates input nodes from the parameter types resolved for the saved function.
    /// <see cref="FunctionType"/> has already resolved generics and variadic parameters,
    /// so the test area reuses it directly instead of inferring from the definition
    /// node's Args.
    /// </summary>
    private async Task RebuildTestArgsAsync(string fullName, bool reload)
    {
        ClearTestArea();
        if (_session is null) return;

        FunctionType? func = await _session.Context.GetNodeTypeAsync<FunctionType>(fullName, reload: reload);
        _functionType = func;

        if (func is null)
        {
            Status = LocaleManager.Instance.Format("wpf.function.status.typefailed", fullName);
            return;
        }
        if (func.Error is not null)
        {
            Status = LocaleManager.Instance.Format("wpf.function.status.invalid", func.Error);
            return;
        }

        foreach (FunctionNodeArgument arg in func.Args)
        {
            IValueTypeAccess? argType = arg.ValueType;
            if (arg.Variadic == true && argType is not null)
                argType = await _session.Context.GetArrayNodeTypeAsync(argType);

            if (argType is null)
            {
                ArgInputs.Add(new FunctionArgInput(arg.Name, arg.Type, arg.Variadic == true, null));
                continue;
            }

            IValueAccess? node = argType.Create(_session.Context);
            if (node is null)
            {
                ArgInputs.Add(new FunctionArgInput(arg.Name, arg.Type, arg.Variadic == true, null));
                continue;
            }

            if (node is IDisposable disposable)
                _session.TrackNode(disposable);

            ArgInputs.Add(new FunctionArgInput(
                arg.Name, arg.Type, arg.Variadic == true, node));
        }

        Status = func.Args.Length == 0
            ? LocaleManager.Instance.Get("wpf.function.status.noargs")
            : LocaleManager.Instance.Format("wpf.function.status.args", func.Args.Length);
    }

    private async void ExecuteCall()
    {
        if (_session is null) return;
        if (Editor.IsNew)
        {
            Status = LocaleManager.Instance.Get("wpf.function.status.savefirst");
            return;
        }

        FunctionType? func = _functionType;
        if (func is null)
        {
            func = await _session.Context.GetNodeTypeAsync<FunctionType>(FunctionName);
            _functionType = func;
        }
        if (func is null)
        {
            Status = LocaleManager.Instance.Get("wpf.function.status.nocall");
            return;
        }

        JsonArray args = [];
        foreach (FunctionArgInput input in ArgInputs)
        {
            if (input.Node is { } node)
            {
                JsonNode? value = null;
                node.TryGetValue(out value);

                // Variadic parameters are expanded at the call site and are not a single
                // array parameter; an empty array means zero actual arguments.
                if (input.IsVariadic)
                {
                    if (value is JsonArray array)
                        foreach (JsonNode? item in array)
                            args.Add(item?.DeepClone());
                    else if (value is not null)
                        args.Add(value.DeepClone());
                }
                else
                    args.Add(value?.DeepClone());
            }
            else
            {
                args.Add(ParseLiteral(input.TextValue));
            }
        }

        try
        {
            // Validated when FunctionType is loaded: the return type of a non-system function
            // is never an unresolved generic.
            JsonNode? result = await _session.Api.CallFunctionAsync(FunctionName, args, func.Return.Name);
            ShowResult(result, func);
            Status = result is null
                ? LocaleManager.Instance.Get("wpf.function.status.callednull")
                : LocaleManager.Instance.Get("wpf.function.status.called");
        }
        catch (Exception ex)
        {
            ClearResult();
            ResultText = null;
            Status = LocaleManager.Instance.Format("wpf.function.status.callfailed", ex.Message);
        }
    }

    /// <summary>
    /// Builds a node for the result value from the return type and hands it to SchemaView
    /// for rendering; falls back to raw JSON text when the return type cannot create a node.
    /// </summary>
    private void ShowResult(JsonNode? result, FunctionType func)
    {
        ClearResult();
        if (result is null)
        {
            ResultText = "(null)";
            return;
        }

        ResultText = result.ToJsonString(IndentedJson);
        if (_session is null) return;

        IValueAccess? node = func.Return.Create(_session.Context, result.DeepClone());
        if (node is null) return;

        if (node is IDisposable disposable)
            _session.TrackNode(disposable);

        ResultNode = node;
    }

    private void ClearResult()
    {
        IValueAccess? node = _resultNode;
        ResultNode = null;

        if (node is IDisposable disposable)
        {
            if (_session is null)
                disposable.Dispose();
            else
                _session.ReleaseNode(disposable);
        }
    }

    private void ClearTestArea()
    {
        foreach (FunctionArgInput input in ArgInputs)
        {
            IValueAccess? node = input.Node;
            input.Dispose();
            if (node is IDisposable disposable)
            {
                if (_session is null)
                    disposable.Dispose();
                else
                    _session.ReleaseNode(disposable);
            }
        }
        ArgInputs.Clear();

        ClearResult();
        ResultText = null;
    }

    private static JsonNode? ParseLiteral(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try
        {
            return JsonNode.Parse(text);
        }
        catch
        {
            // Non-JSON text is treated as a plain string, matching the "enter directly"
            // semantics of the input box.
            return JsonValue.Create(text);
        }
    }

    #endregion
}

/// <summary>
/// A single argument input row in the test area.
///
/// When the parameter type can be resolved, <see cref="Node"/> is non-null and is
/// rendered by SchemaView; generic or unresolvable types fall back to
/// <see cref="TextValue"/> (a JSON literal).
/// </summary>
public class FunctionArgInput : BindableBase, IDisposable
{
    private string _textValue = string.Empty;
    private IValueAccess? _node;

    public FunctionArgInput(string name, string typeName, bool isVariadic, IValueAccess? node)
    {
        Name = isVariadic ? LocaleManager.Instance.Format("wpf.function.arg.variadic", name) : name;
        TypeName = typeName;
        IsVariadic = isVariadic;
        Node = node;
    }

    /// <summary>Parameter name (annotated for variadic parameters)</summary>
    public string Name { get; }

    /// <summary>Parameter type name</summary>
    public string TypeName { get; }

    /// <summary>Whether this is a variadic parameter (expanded into multiple actual arguments on call)</summary>
    public bool IsVariadic { get; }

    /// <summary>Parameter value node (owned by the screen-level session, which destroys it in reverse order)</summary>
    public IValueAccess? Node
    {
        get => _node;
        private set
        {
            if (!SetProperty(ref _node, value)) return;
            RaisePropertyChanged(nameof(HasNode));
            RaisePropertyChanged(nameof(HasTextInput));
        }
    }

    /// <summary>Whether there is a typed input control</summary>
    public bool HasNode => _node is not null;

    /// <summary>Whether to fall back to JSON text input</summary>
    public bool HasTextInput => _node is null;

    /// <summary>JSON text input (only used when there is no typed node)</summary>
    public string TextValue
    {
        get => _textValue;
        set => SetProperty(ref _textValue, value);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Clearing the binding lets the view release the wrapper it created; the node itself is
        // released by the screen after this call.
        Node = null;
    }
}
