using System.Windows.Input;
using Prism.Commands;
using Prism.Mvvm;
using SchemaNode.Property.Common;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.WPF.Man.Services;
using SchemaNode.WPF.View;
using SchemaNode.WPF.View.Localization;
using static SchemaNode.Utility.Constant;
using StructType = SchemaNode.Runtime.StructType;

namespace SchemaNode.WPF.Man.ViewModels;

/// <summary>
/// Generic schema editor ViewModel.
///
/// Tied to the screen lifecycle:
///   - The screen ViewModel calls <see cref="Attach"/> in <c>OnNavigatedTo</c> to inject the session for this screen;
///   - Every rebuild first disposes the old Wrapper (unsubscribing), then disposes the old DataNode (screen-level ReleaseNode),
///     and only then creates the new DataNode / Wrapper, guaranteeing the "Wrapper → DataNode" reverse order;
///   - After saving a type triggers a <c>Loaded=false</c> reload, <see cref="ReloadAsync"/> rebuilds it within this screen,
///     so the old DataNode is no longer referenced by any subscription.
///
/// Threading: every Core call (type loading, schema serialization, node creation, wrapper construction and
/// read-back) runs on the thread pool. Only the assignment of the finished Wrapper to the bindable property
/// happens on the dispatcher, so a screen navigation never blocks the UI thread on schema work.
/// </summary>
public class SchemaEditorViewModel : BindableBase, IDisposable
{
    private const string DefinitionTypeName = "system.schema.node.schema";

    private readonly SchemaViewRegistry _registry;
    private SchemaScreenSession? _session;
    private IValueAccess? _node;
    private string? _typeName;
    private bool _isNew;
    private string? _error;

    public SchemaEditorViewModel(SchemaViewRegistry registry)
    {
        _registry = registry;
        LoadCommand = new DelegateCommand(ExecuteLoad, CanLoad)
            .ObservesProperty(() => TypeName);
    }

    /// <summary>Full name of the type to load</summary>
    public string? TypeName
    {
        get => _typeName;
        set => SetProperty(ref _typeName, value);
    }

    /// <summary>Node rendered by SchemaView; the view materializes its wrapper when loaded.</summary>
    public IValueAccess? Node
    {
        get => _node;
        private set => SetProperty(ref _node, value);
    }

    /// <summary>Whether the editor is currently in the "new schema" flow (not yet saved)</summary>
    public bool IsNew
    {
        get => _isNew;
        private set => SetProperty(ref _isNew, value);
    }

    /// <summary>Error of the last load / wrapper build; shown in the pane so a failure is never blank.</summary>
    public string? Error
    {
        get => _error;
        private set => SetProperty(ref _error, value);
    }

    /// <summary>Registry inherited by the editor view and its nested schema views.</summary>
    public SchemaViewRegistry Registry => _registry;

    public ICommand LoadCommand { get; }

    /// <summary>Multi-level cascading enum child-entry editor (shown only when kind = enum)</summary>
    public EnumValuesEditorViewModel EnumEditor { get; } = new();

    /// <summary>Whether to show the enum cascade editor</summary>
    public bool ShowEnumEditor
    {
        get => _showEnumEditor;
        private set => SetProperty(ref _showEnumEditor, value);
    }

    private bool _showEnumEditor;

    /// <summary>
    /// Binds the session for this screen. Must be called before any load operation.
    /// </summary>
    public void Attach(SchemaScreenSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    /// <summary>
    /// Reloads by full type name and rebuilds it within this screen.
    /// <param name="reload">When true, forces the type to be re-read from schema (the post-save Loaded=false scenario)</param>
    /// </summary>
    public async Task ReloadAsync(string? typeName, bool reload = false)
    {
        DisposeNode();
        Error = null;
        TypeName = typeName;

        SchemaScreenSession? session = _session;
        if (string.IsNullOrWhiteSpace(typeName) || session is null)
        {
            Node = null;
            return;
        }

        string name = typeName;
        BuiltNode built = await Task.Run(async () =>
        {
            try
            {
                NodeType? type = await session.Context.GetNodeTypeAsync(name, reload: reload).ConfigureAwait(false);
                if (type is not IValueTypeAccess valueType)
                    return BuiltNode.Missing(LocaleManager.Instance.Format("wpf.typedoc.status.notfound", name));
                return Build(valueType, session, () => null, null, kinds: null);
            }
            catch (Exception ex)
            {
                return new BuiltNode(null, null, false, ex);
            }
        });

        Apply(built, isNew: false);
    }

    /// <summary>
    /// Loads a "definition node" by full schema name and rebuilds it within this screen.
    ///
    /// Difference from <see cref="ReloadAsync"/>: the latter creates a data node of that type (used by demo screens),
    /// whereas this method creates a <c>system.schema.node.schema</c> node seeded with the JSON of the saved NodeSchema.
    /// A JsonNode seed must be used instead of a NodeSchema instance; otherwise StructNode backfilling loses Attach properties.
    /// </summary>
    public async Task ReloadSchemaAsync(string? typeName, bool reload = false)
    {
        DisposeNode();
        Error = null;
        TypeName = typeName;

        SchemaScreenSession? session = _session;
        if (string.IsNullOrWhiteSpace(typeName) || session is null)
        {
            Node = null;
            return;
        }

        string name = typeName;
        BuiltNode built = await Task.Run(async () =>
        {
            try
            {
                NodeSchema[] schemas = await session.Api.GetSchemaAsync([name]).ConfigureAwait(false);
                NodeSchema? schema = schemas.FirstOrDefault(s =>
                                        s.FullName.Equals(name, StringComparison.OrdinalIgnoreCase))
                                    ?? schemas.FirstOrDefault();
                if (schema is null)
                    return BuiltNode.Missing(LocaleManager.Instance.Format("wpf.typedoc.status.notfound", name));

                StructType? schemaType = await session.Context
                    .GetNodeTypeAsync<StructType>(DefinitionTypeName).ConfigureAwait(false);
                if (schemaType is null)
                    return BuiltNode.Missing(LocaleManager.Instance.Get("wpf.typedoc.status.notype"));

                string[] kinds = await SchemaKindCatalog.GetNodeKindsAsync(session.Context).ConfigureAwait(false);
                return Build(schemaType, session, () => session.Context.ToSchemaJson(schema), schema, kinds);
            }
            catch (Exception ex)
            {
                return new BuiltNode(null, null, false, ex);
            }
        });

        Apply(built, isNew: false);
    }

    /// <summary>
    /// Creates a new schema: replicates handleNew from schema.vue.
    /// Creates a node of type <c>system.schema.node.schema</c> with <paramref name="seed"/> as the seed,
    /// and writes the kind whitelist computed by <see cref="SchemaKindCatalog"/> into the <c>kind</c> field.
    /// </summary>
    public async Task NewSchemaAsync(NodeSchema? seed = null)
    {
        DisposeNode();
        Error = null;

        SchemaScreenSession? session = _session;
        if (session is null) return;

        NodeSchema init = seed ?? new NodeSchema();
        BuiltNode built = await Task.Run(async () =>
        {
            try
            {
                StructType? schemaType = await session.Context
                    .GetNodeTypeAsync<StructType>(DefinitionTypeName).ConfigureAwait(false);
                if (schemaType is null)
                    return BuiltNode.Missing(LocaleManager.Instance.Get("wpf.typedoc.status.notype"));

                string[] kinds = await SchemaKindCatalog.GetNodeKindsAsync(session.Context).ConfigureAwait(false);
                return Build(schemaType, session, () => init, null, kinds);
            }
            catch (Exception ex)
            {
                return new BuiltNode(null, null, false, ex);
            }
        });

        if (built.Node is null)
        {
            Apply(built, isNew: true);
            return;
        }

        // The new schema has no saved identity yet, so the screen must not address it by name.
        TypeName = null;
        Apply(built, isNew: true);
    }

    /// <summary>
    /// NodeSchema corresponding to the current edit result (null when not yet bound or incomplete).
    /// </summary>
    public NodeSchema? GetNodeSchema() => _node?.GetValue<NodeSchema>();

    /// <summary>
    /// Background read-back for the save path: walking a definition node is Core work and must not
    /// run on the dispatcher.
    /// </summary>
    public Task<NodeSchema?> GetNodeSchemaAsync() => Task.Run(() => _node?.GetValue<NodeSchema>());

    /// <summary>
    /// Clears the editor and releases the node tree for this screen.
    /// </summary>
    public void Clear()
    {
        DisposeNode();
        Node = null;
        Error = null;
        TypeName = null;
        IsNew = false;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeNode();
        EnumEditor.Dispose();
        _session = null; // The session is disposed centrally by the screen ViewModel
    }

    private bool CanLoad() => !string.IsNullOrWhiteSpace(_typeName);

    private async void ExecuteLoad() => await ReloadAsync(_typeName);

    /// <summary>
    /// Runs on the thread pool: creates the node, applies the kind whitelist, builds the whole wrapper
    /// tree and reads the definition back so no Core work remains for the dispatcher.
    /// </summary>
    private BuiltNode Build(
        IValueTypeAccess type,
        SchemaScreenSession session,
        Func<object?> seedFactory,
        NodeSchema? schema,
        string[]? kinds)
    {
        object? seed = seedFactory();
        IValueAccess? node = seed is null ? type.Create(session.Context) : type.Create(session.Context, seed);
        if (node is null) return new BuiltNode(null, schema, false, null);

        try
        {
            if (kinds is not null && node.GetAccessValue("kind") is { } kindField)
                kindField.SetPropertyValue<WhiteList, object[]>(kinds.Cast<object>().ToArray(), null, null);

            // The view materializes the wrapper when it renders this node; the editor only owns the node.
            return new BuiltNode(
                node,
                node.GetValue<NodeSchema>(),
                node.GetAccessValue("enum") is not null,
                null);
        }
        catch (Exception ex)
        {
            if (node is IDisposable disposable) disposable.Dispose();
            return new BuiltNode(null, schema, false, ex);
        }
    }

    /// <summary>Dispatcher-side part: adopt the prepared node and show the enum panel when applicable.</summary>
    private void Apply(BuiltNode built, bool isNew)
    {
        if (built.Node is null)
        {
            Node = null;
            Error = built.Error?.Message;
            return;
        }

        Node = built.Node;
        if (built.Node is IDisposable disposable)
            _session?.TrackNode(disposable);

        Error = null;
        IsNew = isNew;
        UpdateEnumEditor(built.Schema, built.HasEnumField);
    }

    private sealed record BuiltNode(
        IValueAccess? Node,
        NodeSchema? Schema,
        bool HasEnumField,
        Exception? Error)
    {
        public static BuiltNode Missing(string message)
            => new(null, null, false, new InvalidOperationException(message));
    }

    /// <summary>
    /// Whether to show the cascade editor is determined by the Attach result: it is shown only when kind = enum
    /// and an <c>enum</c> property field is attached; the toggle logic for the enum-specific panel is not hard-coded in the view.
    /// </summary>
    private void UpdateEnumEditor(NodeSchema? schema, bool hasEnumField)
    {
        // Only definition nodes (system.schema.node.schema) parse a NodeSchema;
        // demo screens create ordinary data nodes, so schema read-back must not be done here.
        if (_node?.Type.Name.Equals(DefinitionTypeName, StringComparison.OrdinalIgnoreCase) != true)
        {
            ShowEnumEditor = false;
            EnumEditor.Detach();
            return;
        }

        bool isEnum = schema?.Kind?.Equals(NODE_KIND_ENUM, StringComparison.OrdinalIgnoreCase) == true
                      && hasEnumField;
        string fullName = schema is null ? string.Empty : schema.FullName;

        if (!isEnum || _session is null || string.IsNullOrWhiteSpace(fullName))
        {
            ShowEnumEditor = false;
            EnumEditor.Detach();
            return;
        }

        ShowEnumEditor = true;
        EnumEditor.Attach(_session.Context, fullName);
    }

    /// <summary>
    /// Releases the node. Clearing <see cref="Node"/> first lets the bound view dispose the wrapper
    /// it created, so the reverse order stays Wrapper → DataNode even though the view owns the wrapper.
    /// </summary>
    private void DisposeNode()
    {
        ShowEnumEditor = false;
        EnumEditor.Detach();

        IValueAccess? node = _node;
        Node = null;

        if (node is IDisposable disposable)
        {
            if (_session is null)
                disposable.Dispose();
            else
                _session.ReleaseNode(disposable);
        }
    }
}
