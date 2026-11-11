using System.ComponentModel;
using System.Windows.Input;
using Avalonia.Threading;
using SchemaNode.Avalonia.View.Localization;
using SchemaNode.Node;
using SchemaNode.Property.Common;
using SchemaNode.Property.Struct;
using SchemaNode.Runtime;
using SchemaNode.Struct;

namespace SchemaNode.Avalonia.View.NodeWrapper;

/// <summary>
/// Adapts a Core <see cref="DataNode"/> into an Avalonia-bindable object.
///
/// Responsibilities shared by every component wrapper:
///   - pushes <c>Value</c>, visibility and validation state to the UI;
///   - marshals every subscription callback back to the UI dispatcher before touching a CLR
///     property (Core may raise notifications from any thread);
///   - owns its Rx subscriptions and disposes them exactly once.
/// </summary>
public abstract class DataNodeWrapper : INotifyPropertyChanged, IDisposable
{
    private readonly List<IDisposable> _subscriptions = [];
    private readonly EventHandler _languageChangedHandler;
    private bool _disposed;
    private string? _inputError;
    private string? _templateKeyOverride;
    private bool _readOnly;
    private bool _disabled;
    private bool _forcedReadOnly;
    private bool _require;
    private bool _visible;

    protected DataNodeWrapper(DataNode node, SchemaViewRegistry? registry, string? fieldName, SchemaViewRole role)
    {
        NodeImpl = node ?? throw new ArgumentNullException(nameof(node));
        Registry = registry ?? SchemaViewRegistry.CreateDefault();
        FieldName = fieldName;
        Role = role;

        Track(NodeImpl.Subscribe(OnDataChanged, immediate: true));
        Track(NodeImpl.SubscribeViolated(OnViolatedChanged, immediate: true));
        // Visibility is caller-facing rather than node-local: a relation that hides a struct or
        // array element must also hide the cells it owns, and a table renders those leaf nodes
        // directly. Subscribe to every ancestor so an inherited hide reaches this wrapper too.
        _visible = IsNodeVisible(NodeImpl);
        Track(NodeImpl.SubscribeAncestorProperty<Visible, bool>(OnVisibleChanged, immediate: true));
        Track(NodeImpl.SubscribeAncestorProperty<InVisible, bool>(OnVisibleChanged, immediate: true));
        Track(NodeImpl.SubscribeAncestorProperty<ReadOnly, bool>(OnReadOnlyChanged, immediate: true));
        Track(NodeImpl.SubscribeAncestorProperty<Disable, bool>(OnDisabledChanged, immediate: true));
        Track(NodeImpl.SubscribeProperty<DisplayOnly, bool>(OnDisplayOnlyChanged, immediate: true));

        // Require is a permission-style constraint, not an entry-source concern: Date and Bool
        // need it too, so it belongs to the common base instead of an input-only wrapper.
        Track(NodeImpl.SubscribeProperty<Require, bool>(OnRequireChanged, immediate: true));

        // Cached labels must follow a language switch without rebuilding the visual tree.
        _languageChangedHandler = OnLanguageChangedHandler;
        LocaleManager.Instance.LanguageChanged += _languageChangedHandler;
    }

    #region Identity

    /// <summary>Underlying Core node.</summary>
    public DataNode Node => NodeImpl;

    /// <summary>The registry this wrapper and its descendants resolve views through.</summary>
    public SchemaViewRegistry Registry { get; }

    /// <summary>Render role (field / table cell).</summary>
    public SchemaViewRole Role { get; }

    /// <summary>Schema field name, when the node is a struct field.</summary>
    public string? FieldName { get; }

    /// <summary>Core node used by derived wrappers.</summary>
    protected DataNode NodeImpl { get; }

    /// <summary>Whether the wrapper has been disposed.</summary>
    public bool IsDisposed => _disposed;

    #endregion

    #region Bindable state

    /// <summary>
    /// Node value. The setter converts the incoming editor value; a conversion failure is
    /// surfaced through <see cref="Error"/> instead of silently dropping the edit.
    /// </summary>
    public object? Value
    {
        get => ReadEditorValue();
        set
        {
            if (_disposed) return;
            WriteEditorValue(value);
        }
    }

    /// <summary>
    /// Value the editor should display. Text-like wrappers override this so an uncommitted
    /// editor text survives a node notification instead of being replaced by the node value.
    /// </summary>
    protected virtual object? ReadEditorValue() => NodeImpl.GetValue<object>();

    /// <summary>Commits an editor value. Overridden by wrappers that keep a pending input.</summary>
    protected virtual void WriteEditorValue(object? value)
    {
        bool ok = NodeImpl.TrySetValue(value);
        SetInputError(ok
            ? null
            : LocaleManager.Instance.Format("avalonia.error.value.convert", value, NodeImpl.Type.Name));
    }

    /// <summary>
    /// Whether the node should be rendered. Core computes each node from Visible / InVisible, and
    /// a hidden ancestor hides everything it owns.
    /// </summary>
    public bool IsVisible => _visible;

    /// <summary>Whether any ancestor or the node itself marks the field read-only.</summary>
    public bool IsReadOnly => _readOnly;

    /// <summary>Whether any ancestor or the node itself disables the field.</summary>
    public bool IsDisabled => _disabled;

    /// <summary>Whether the owner of this wrapper forces the subtree to render read-only.</summary>
    public bool IsForcedReadOnly => _forcedReadOnly;

    /// <summary>The field is meant for display only.</summary>
    public bool IsDisplayOnly => NodeImpl.DisplayOnly;

    /// <summary>Editors are enabled only when not read-only, disabled, forced read-only or display-only.</summary>
    public bool IsEditable => !_readOnly && !_disabled && !_forcedReadOnly && !NodeImpl.DisplayOnly;

    /// <summary>Whether the field is required.</summary>
    public bool Require => _require;

    /// <summary>Whether the field requires a value.</summary>
    public bool IsRequired => _require;

    /// <summary>Whether the field accepts an unset value.</summary>
    public bool IsOptional => !_require;

    /// <summary>Display label (localized Display property, schema name or field name).</summary>
    public string Label
    {
        get
        {
            LocaleString? display = NodeImpl.GetPropertyValue<Display, LocaleString>();
            string text = LocalizedDisplay.Resolve(NodeImpl.Context, display, NodeImpl.Name, FieldName, NodeImpl.Type.Name);
            return string.IsNullOrWhiteSpace(text) ? NodeImpl.Type.Name : text;
        }
    }

    /// <summary>Inline error text; hidden in compact roles such as table cells.</summary>
    public string? Error
    {
        get
        {
            if (_inputError is not null) return _inputError;
            if (NodeImpl.IsValid) return null;
            return ResolveMessage(NodeImpl.Error);
        }
    }

    /// <summary>
    /// Turns a Core failure message into display text. Core returns the locale key of the failing
    /// rule when a constraint carries no message of its own, so the view must translate it and,
    /// failing that, substitute the generic localized message instead of showing a raw code.
    /// </summary>
    private static string ResolveMessage(string? message)
    {
        if (message is not null && LocaleManager.Instance.TryGet(message, out string translated))
            return translated;
        if (message is not null && !LooksLikeStatusCode(message))
            return message;
        return LocaleManager.Instance.Get("avalonia.view.error.invalid");
    }

    /// <summary>Whether the text is an untranslated locale key such as <c>VALUE_NOT_VALID</c>.</summary>
    private static bool LooksLikeStatusCode(string text)
    {
        if (text.Length == 0) return false;
        foreach (char c in text)
        {
            if (!char.IsAsciiLetterUpper(c) && !char.IsAsciiDigit(c) && c != '_' && c != '.')
                return false;
        }
        return true;
    }

    /// <summary>
    /// Whether this wrapper renders a concrete editable value. Containers (struct, array, table)
    /// own children that carry the real failure, so an error block on the container only repeats it
    /// without naming the offending field. The error state itself stays on <see cref="Error"/> /
    /// <see cref="HasErrors"/> for hosts that gate saving or summarize validation.
    /// </summary>
    protected virtual bool RendersValue => true;

    /// <summary>Whether the error should be rendered inline below the editor.</summary>
    public bool ShowInlineError => RendersValue && Role == SchemaViewRole.Field;

    /// <summary>Whether an inline error block should be visible.</summary>
    public bool ShowError => ShowInlineError && HasErrors;

    /// <summary>
    /// Hover fallback used only by roles that hide the inline block (table cells, row editors).
    /// A field that already renders the message inline must not repeat it through a tooltip.
    /// </summary>
    public string? ErrorTip => !ShowInlineError && RendersValue ? Error : null;

    /// <summary>Remove action installed by the parent array/table wrapper (null when not a row).</summary>
    public ICommand? RemoveCommand { get; internal set; }

    /// <summary>
    /// View key this wrapper renders through by default. Derived wrappers override it so an
    /// unknown node kind still reaches the matching view instead of falling back to the input view.
    /// </summary>
    public virtual string? DefaultTemplateKey => null;

    /// <summary>Host-supplied view override; wins over registry lookup when set.</summary>
    public string? TemplateKeyOverride
    {
        get => _templateKeyOverride;
        set
        {
            if (string.Equals(_templateKeyOverride, value, StringComparison.Ordinal)) return;
            _templateKeyOverride = value;
            RaisePropertyChanged(nameof(TemplateKeyOverride));
        }
    }

    #endregion

    #region INotifyPropertyChanged

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raises a property change on the UI thread.</summary>
    protected void RaisePropertyChanged(string propertyName)
    {
        Post(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
    }

    /// <summary>Raises a property change on the current (already UI) thread.</summary>
    protected void RaisePropertyChangedNow(string propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    #endregion

    #region Validation state

    /// <summary>
    /// Whether this node currently fails a constraint or holds an uncommitted invalid edit.
    /// The view renders this through <see cref="ShowError"/>; the wrapper deliberately does not
    /// implement <c>INotifyDataErrorInfo</c>, because Avalonia would then also paint the binding
    /// target's <c>DataValidationErrors</c> and the same message would appear twice.
    /// </summary>
    public bool HasErrors => _inputError is not null || !NodeImpl.IsValid;

    #endregion

    #region Threading / subscriptions

    /// <summary>
    /// Schedules an action on the UI dispatcher. Never uses a blocking send, so a UI-thread wait
    /// can never deadlock against a Core continuation.
    /// </summary>
    protected void Post(Action action)
    {
        if (_disposed) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed) return;
            action();
        });
    }

    /// <summary>Registers a subscription so it is disposed with this wrapper.</summary>
    protected void Track(IDisposable subscription)
    {
        if (_disposed)
        {
            subscription.Dispose();
            return;
        }
        _subscriptions.Add(subscription);
    }

    /// <summary>Hook invoked on the Core notification thread before the value change is posted.</summary>
    protected virtual void OnValueChanged()
    {
    }

    private void OnDataChanged((IValueAccess node, object? value) args)
    {
        if (_disposed) return;
        OnValueChanged();
        Post(() => RaisePropertyChangedNow(nameof(Value)));
    }

    private void OnViolatedChanged((IValueAccess node, bool isValid) args)
    {
        if (_disposed) return;
        RaiseErrorStateChanged();
    }

    private void OnVisibleChanged((IValueAccess node, Type propertyType, bool[] values) args)
    {
        if (_disposed) return;
        bool visible = IsNodeVisible(NodeImpl);
        if (_visible == visible) return;
        _visible = visible;
        RaisePropertyChanged(nameof(IsVisible));
    }

    /// <summary>Whether the node and all of its ancestors allow rendering.</summary>
    private static bool IsNodeVisible(DataNode node)
    {
        for (DataNode? current = node; current is not null; current = current.Parent as DataNode)
            if (!current.Visible) return false;
        return true;
    }

    private void OnReadOnlyChanged((IValueAccess node, Type propertyType, bool[] values) args)
    {
        if (_disposed) return;
        bool readOnly = args.values.Any(v => v);
        if (_readOnly == readOnly) return;
        _readOnly = readOnly;
        RaisePropertyChanged(nameof(IsReadOnly));
        RaisePropertyChanged(nameof(IsEditable));
        OnEditableChanged();
    }

    private void OnDisabledChanged((IValueAccess node, Type propertyType, bool[] values) args)
    {
        if (_disposed) return;
        bool disabled = args.values.Any(v => v);
        if (_disabled == disabled) return;
        _disabled = disabled;
        RaisePropertyChanged(nameof(IsDisabled));
        RaisePropertyChanged(nameof(IsEditable));
        OnEditableChanged();
    }

    private void OnDisplayOnlyChanged((IValueAccess node, Type propertyType, bool value, bool oldValue) args)
    {
        if (_disposed) return;
        RaisePropertyChanged(nameof(IsDisplayOnly));
        RaisePropertyChanged(nameof(IsEditable));
        OnEditableChanged();
    }

    /// <summary>
    /// Forces this wrapper subtree to render read-only. Used by a table that switches to row-editor
    /// mode: cells become display-only while the row editor still owns the real node.
    /// </summary>
    public void SetForcedReadOnly(bool value)
    {
        if (_disposed || _forcedReadOnly == value) return;
        _forcedReadOnly = value;
        RaisePropertyChanged(nameof(IsForcedReadOnly));
        RaisePropertyChanged(nameof(IsEditable));
        OnForcedReadOnlyChanged(value);
        OnEditableChanged();
    }

    /// <summary>Hook for composite wrappers that must forward the forced state to their children.</summary>
    protected virtual void OnForcedReadOnlyChanged(bool value)
    {
    }

    /// <summary>Hook invoked after any source of <see cref="IsEditable"/> changes.</summary>
    protected virtual void OnEditableChanged()
    {
    }

    /// <summary>Hook invoked when the required constraint changes, for editors that branch on it.</summary>
    protected virtual void OnRequireChanged((IValueAccess node, Type propertyType, bool value, bool oldValue) args)
    {
        if (_disposed) return;
        bool require = args.value;
        if (_require == require) return;
        _require = require;
        RaisePropertyChanged(nameof(Require));
        RaisePropertyChanged(nameof(IsRequired));
        RaisePropertyChanged(nameof(IsOptional));
    }

    /// <summary>Hook for wrappers that cache localized collections (for example table columns).</summary>
    protected virtual void OnLanguageChanged()
    {
    }

    private void OnLanguageChangedHandler(object? sender, EventArgs e)
    {
        if (_disposed) return;
        OnLanguageChanged();
        RaisePropertyChanged(nameof(Label));
        RaisePropertyChanged(nameof(Error));
        RaisePropertyChanged(nameof(ErrorTip));
    }

    /// <summary>Sets (or clears) the value-conversion / entry-loading error shown by the view.</summary>
    protected void SetInputError(string? error)
    {
        if (string.Equals(_inputError, error, StringComparison.Ordinal)) return;
        _inputError = error;
        RaiseErrorStateChanged();
    }

    private void RaiseErrorStateChanged()
    {
        Post(() =>
        {
            RaisePropertyChangedNow(nameof(HasErrors));
            RaisePropertyChangedNow(nameof(ShowError));
            RaisePropertyChangedNow(nameof(Error));
            RaisePropertyChangedNow(nameof(ErrorTip));
        });
    }

    #endregion

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Disposes subscriptions; idempotent.</summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;

        LocaleManager.Instance.LanguageChanged -= _languageChangedHandler;
        for (int i = _subscriptions.Count - 1; i >= 0; i--)
            _subscriptions[i].Dispose();
        _subscriptions.Clear();
    }

    #endregion
}
