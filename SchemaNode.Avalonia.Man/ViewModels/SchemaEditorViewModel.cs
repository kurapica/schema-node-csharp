using System.Text.Json.Nodes;
using System.Windows.Input;
using SchemaNode.Avalonia.Man.Infrastructure;
using SchemaNode.Node;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Schema.Provider;
using SchemaNode.Avalonia.Man.Services;
using SchemaNode.Avalonia.View;
using SchemaNode.Avalonia.View.Localization;
using static SchemaNode.Utility.Constant;
using StructType = SchemaNode.Runtime.StructType;

namespace SchemaNode.Avalonia.Man.ViewModels;

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
    // The definition the screen opened; kept across the AI apply so the write-back guard and the
    // field gate use the same Man rule on the same schema.
    private NodeSchema? _definitionSchema;
    private string? _error;
    private CancellationTokenSource? _publishCancellation;
    private bool _disposed;

    public SchemaEditorViewModel(SchemaViewRegistry registry)
    {
        _registry = registry;
        LoadCommand = new DelegateCommand(ExecuteLoad, CanLoad)
            .ObservesProperty(() => TypeName);
    }

    /// <summary>
    /// Optional non-blocking delay between building the node on the thread pool and publishing it
    /// to the view. Relations write their properties asynchronously (the call process is debounced),
    /// so a screen that binds the root view immediately materializes elements for fields that are
    /// about to be hidden. Zero by default; only the type-definition screen opts in.
    ///
    /// The wait is a <see cref="Task.Delay(TimeSpan, CancellationToken)"/> and is abandoned by
    /// <see cref="DisposeNode"/> / <see cref="Dispose"/>, so navigation or a newer reload never
    /// publishes (or leaks) the node it was about to show.
    /// </summary>
    public TimeSpan NodePublishDelay { get; set; }

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
        // A screen may attach again after a teardown (same instance reuse); a fresh session
        // always starts with a live editor.
        _disposed = false;
        CancelPendingPublish();
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
                SchemaNode.Runtime.NodeType? type = await session.Context.GetNodeTypeAsync(name, reload: reload).ConfigureAwait(false);
                if (type is not IValueTypeAccess valueType)
                    return BuiltNode.Missing(LocaleManager.Instance.Format("avalonia.typedoc.status.notfound", name));
                return Build(valueType, session, () => null, null, kinds: null);
            }
            catch (Exception ex)
            {
                return new BuiltNode(null, null, false, ex);
            }
        });

        if (!await DelayPublishAsync())
        {
            DisposeUnpublished(built);
            return;
        }

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
                NodeSchema? schema = await LoadDefinitionAsync(session, name, reload).ConfigureAwait(false);
                if (schema is null)
                    return BuiltNode.Missing(LocaleManager.Instance.Format("avalonia.typedoc.status.notfound", name));

                StructType? schemaType = await session.Context
                    .GetNodeTypeAsync<StructType>(DefinitionTypeName).ConfigureAwait(false);
                if (schemaType is null)
                    return BuiltNode.Missing(LocaleManager.Instance.Get("avalonia.typedoc.status.notype"));

                string[] kinds = await SchemaKindCatalog.GetNodeKindsAsync(session.Context).ConfigureAwait(false);
                return Build(schemaType, session, () => session.Context.ToSchemaJson(schema), schema, kinds);
            }
            catch (Exception ex)
            {
                return new BuiltNode(null, null, false, ex);
            }
        });

        if (!await DelayPublishAsync())
        {
            DisposeUnpublished(built);
            return;
        }

        Apply(built, isNew: false);
        _definitionSchema = built.Source;
    }

    /// <summary>
    /// Loads the <see cref="NodeSchema"/> a definition screen should edit.
    ///
    /// <para>
    /// The API returns a namespace tree: the requested namespace entry carries its own definition
    /// (the one holding <c>name</c>, <c>namespace</c> and <c>auths</c>) and nests its direct children.
    /// The definition is therefore located by <see cref="SchemaTreeSearch.Find"/> rather than by
    /// assuming a flat array of children.
    /// </para>
    /// </summary>
    private static async Task<NodeSchema?> LoadDefinitionAsync(SchemaScreenSession session, string name, bool reload)
    {
        if (reload)
            await session.Context.GetNodeTypeAsync(name, reload: true).ConfigureAwait(false);

        NodeSchema[] schemas = await session.Api.GetSchemaAsync([name]).ConfigureAwait(false);
        return SchemaTreeSearch.Find(schemas, name) ?? schemas.FirstOrDefault();
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
                    return BuiltNode.Missing(LocaleManager.Instance.Get("avalonia.typedoc.status.notype"));

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

        if (!await DelayPublishAsync())
        {
            DisposeUnpublished(built);
            return;
        }

        // The new schema has no saved identity yet, so the screen must not address it by name.
        TypeName = null;
        Apply(built, isNew: true);
        _definitionSchema = null;
    }

    /// <summary>
    /// Replaces the definition node with a complete NodeSchema payload produced by the assistant.
    /// <para>
    /// The payload is used as a <see cref="JsonNode"/> seed, not as a materialized
    /// <see cref="NodeSchema"/> instance, so the Attach driven property fields are rebuilt the same
    /// way as a normal schema reload. The whole node is replaced, so fields and relations refresh
    /// together. The new node is tracked by the screen session and disposed in reverse order.
    /// </para>
    /// </summary>
    /// <param name="schemaJson">The complete normalized NodeSchema payload.</param>
    /// <param name="isNew">Whether the editor stays in the new-schema flow.</param>
    /// <returns>True when the node was rebuilt.</returns>
    public async Task<bool> ApplySchemaAsync(JsonNode schemaJson, bool isNew)
    {
        ArgumentNullException.ThrowIfNull(schemaJson);

        // Capture the current definition before the node is disposed; this is also the source of
        // the protected values that an AI payload must not overwrite.
        NodeSchema? definitionSource = isNew ? null : _definitionSchema;

        // Same Man rule as the manual gate: an AI payload must not be written back onto a
        // definition the screens currently treat as read-only.
        if (definitionSource is not null && !SchemaDefinitionEditRule.IsDefinitionEditable(definitionSource))
        {
            Error = LocaleManager.Instance.Get("avalonia.typedoc.status.systemprotected");
            return false;
        }

        DisposeNode();
        Error = null;

        SchemaScreenSession? session = _session;
        if (session is null) return false;

        BuiltNode built = await Task.Run(async () =>
        {
            try
            {
                StructType? schemaType = await session.Context
                    .GetNodeTypeAsync<StructType>(DefinitionTypeName).ConfigureAwait(false);
                if (schemaType is null)
                    return BuiltNode.Missing(LocaleManager.Instance.Get("avalonia.typedoc.status.notype"));

                string[] kinds = await SchemaKindCatalog.GetNodeKindsAsync(session.Context).ConfigureAwait(false);

                // Only fields the Man rule allows are taken from the assistant payload. Protected
                // fields stay at the value of the live definition, so a whole-payload apply cannot
                // silently rewrite name/kind/display on a system namespace.
                JsonNode seed = schemaJson;
                if (definitionSource is not null)
                {
                    if (schemaJson is not JsonObject)
                        return BuiltNode.Missing(LocaleManager.Instance.Get("avalonia.typedoc.status.noschema"));

                    JsonNode? current = session.Context.ToSchemaJson(definitionSource);
                    if (current is not JsonObject currentObject)
                        return BuiltNode.Missing(LocaleManager.Instance.Get("avalonia.typedoc.status.noschema"));

                    seed = MergeEditableDefinitionPayload((JsonObject)schemaJson, currentObject, definitionSource);
                }

                BuiltNode result = Build(schemaType, session, () => seed, null, kinds);
                // The rebuilt node is gone; re-apply the same field rule to the new instance.
                if (definitionSource is not null && result.Node is not null)
                    RestrictDefinition(result.Node, definitionSource);
                return result;
            }
            catch (Exception ex)
            {
                return new BuiltNode(null, null, false, ex);
            }
        });

        if (!await DelayPublishAsync())
        {
            DisposeUnpublished(built);
            return false;
        }

        Apply(built, isNew);
        if (isNew)
            _definitionSchema = null;

        if (built.Node is null) return false;

        // Adopt the generated identity so the save path and the header address the new type.
        TypeName = built.Schema?.FullName;
        return true;
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
    /// Reads the saved definition for a full name without publishing it. The copy flow needs the
    /// stored definition, not the live editor content, so unsaved changes and read-only wrapper
    /// state never travel into the new type.
    /// </summary>
    public Task<NodeSchema?> LoadSavedDefinitionAsync(string name)
    {
        SchemaScreenSession? session = _session;
        if (session is null || string.IsNullOrWhiteSpace(name))
            return Task.FromResult<NodeSchema?>(null);

        return Task.Run(() => LoadDefinitionAsync(session, name, reload: false));
    }

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
        _definitionSchema = null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
        CancelPendingPublish();
        DisposeNode();
        EnumEditor.Dispose();
        _session = null; // The session is disposed centrally by the screen ViewModel
    }

    private void CancelPendingPublish()
    {
        _publishCancellation?.Cancel();
        _publishCancellation = null;
    }

    /// <summary>
    /// Waits for the relation writes to settle before the node is published to the view.
    /// Returns false when the wait was abandoned (navigation, newer reload, disposed editor);
    /// the caller then disposes the never-published node.
    /// </summary>
    private async Task<bool> DelayPublishAsync()
    {
        if (NodePublishDelay <= TimeSpan.Zero)
            return !_disposed;

        CancellationTokenSource cancellation = new();
        _publishCancellation = cancellation;
        try
        {
            await Task.Delay(NodePublishDelay, cancellation.Token);
            return !_disposed;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            if (ReferenceEquals(_publishCancellation, cancellation))
                _publishCancellation = null;
            cancellation.Dispose();
        }
    }

    /// <summary>Releases a node that was built but never handed to the screen session.</summary>
    private static void DisposeUnpublished(BuiltNode built)
    {
        if (built.Node is IDisposable disposable)
            disposable.Dispose();
    }

    private bool CanLoad() => !string.IsNullOrWhiteSpace(_typeName);

    /// <summary>
    /// Marks the definition fields that Man's edit rule does not allow as read-only on the node
    /// instance. The flag is instance-scoped: it is not written back to the type metadata, so the
    /// system schema definition stays untouched and other screens are unaffected.
    /// <para>
    /// For a system-defined namespace only the <c>auths</c> field stays editable; every other
    /// field, including <c>namespace</c> (which Core already locks through
    /// <c>Relation&lt;ReadOnly, Call&gt;(NODE_SELF, notempty(@Name))</c> once <c>name</c> has a value),
    /// is locked. Other system-defined kinds are fully locked while the rule's global switch is off,
    /// and everything is editable once it is turned on.
    /// </para>
    /// </summary>
    private static void RestrictDefinition(IValueAccess node, NodeSchema schema)
    {
        if (node is not StructNode structNode) return;
        foreach ((StructFieldType field, IValueAccess value) in structNode.GetFields())
        {
            if (SchemaDefinitionEditRule.IsFieldEditable(schema, field.Name)) continue;
            value.SetPropertyValue<ReadOnly, bool>(true, null, null);
        }
    }

    /// <summary>
    /// Merges an assistant payload onto the current definition while keeping every field the Man
    /// rule protects. Editable fields (including removals) come from the payload; the rest keeps the
    /// live definition value, so an apply can never silently rewrite a protected field.
    /// </summary>
    private static JsonObject MergeEditableDefinitionPayload(
        JsonObject payload,
        JsonObject current,
        NodeSchema source)
    {
        JsonObject merged = (JsonObject)current.DeepClone();

        foreach (string key in merged.Select(pair => pair.Key).ToArray())
        {
            if (SchemaDefinitionEditRule.IsFieldEditable(source, key))
                merged.Remove(key);
        }

        foreach ((string key, JsonNode? value) in payload)
        {
            if (!SchemaDefinitionEditRule.IsFieldEditable(source, key)) continue;
            merged[key] = value?.DeepClone();
        }

        return merged;
    }

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
        if (node is null) return new BuiltNode(null, schema, false, null, schema);

        try
        {
            if (kinds is not null && node.GetAccessValue("kind") is { } kindField)
                kindField.SetPropertyValue<WhiteList, object[]>(kinds.Cast<object>().ToArray(), null, null);

            // Man's edit rule is the single decision point for field editability: a system-defined
            // namespace keeps only `auths` editable (so permissions can be configured) and every
            // other system-defined kind is fully locked; the whole gate disappears when the rule's
            // global switch is on. This is a per-field gate (each field wrapper derives its
            // IsEditable from the node's ReadOnly property), not a whole-screen switch. Core still
            // locks `namespace` on its own through the Relation<ReadOnly, Call>(NODE_SELF,
            // notempty(@Name)) once `name` has a value.
            if (schema is not null)
                RestrictDefinition(node, schema);

            // The view materializes the wrapper when it renders this node; the editor only owns the node.
            return new BuiltNode(
                node,
                node.GetValue<NodeSchema>(),
                node.GetAccessValue("enum") is not null,
                null,
                schema);
        }
        catch (Exception ex)
        {
            if (node is IDisposable disposable) disposable.Dispose();
            return new BuiltNode(null, schema, false, ex, schema);
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
        Exception? Error,
        NodeSchema? Source = null)
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
        CancelPendingPublish();
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
