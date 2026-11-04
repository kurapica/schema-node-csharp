using System.Collections;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using SchemaNode.Node;
using SchemaNode.Property.Common;
using SchemaNode.Property.Struct;
using SchemaNode.Runtime;
using SchemaNode.Struct;
using SchemaNode.WPF.View.Localization;

namespace SchemaNode.WPF.View.NodeWrapper;

/// <summary>
/// Adapts a Core <see cref="DataNode"/> into a WPF-bindable object.
///
/// Responsibilities shared by every component wrapper:
///   - pushes <c>Value</c>, visibility and validation state to the UI;
///   - marshals every subscription callback back to the dispatcher before touching a CLR
///     property (Core may raise notifications from any thread);
///   - owns its Rx subscriptions and disposes them exactly once.
///
/// The wrapper tree is built by <see cref="NodeWrapperFactory"/>. A node value change never
/// rebuilds the subtree: children listen to their own nodes and only collections (children,
/// rows, entries) are reconciled when the node identity set changes.
/// </summary>
public abstract class DataNodeWrapper : INotifyPropertyChanged, INotifyDataErrorInfo, IDisposable
{
    private readonly List<IDisposable> _subscriptions = [];
    private readonly Dispatcher _dispatcher;
    private readonly EventHandler _languageChangedHandler;
    private bool _disposed;
    private string? _inputError;
    private string? _templateKeyOverride;
    private bool _readOnly;
    private bool _disabled;
    private bool _forcedReadOnly;

    protected DataNodeWrapper(DataNode node, SchemaViewRegistry? registry, string? fieldName, SchemaViewRole role)
    {
        NodeImpl = node ?? throw new ArgumentNullException(nameof(node));
        Registry = registry ?? SchemaViewRegistry.CreateDefault();
        FieldName = fieldName;
        Role = role;
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        Track(NodeImpl.Subscribe(OnDataChanged, immediate: true));
        Track(NodeImpl.SubscribeViolated(OnViolatedChanged, immediate: true));
        Track(NodeImpl.SubscribeProperty<Visible, bool>(OnVisibleChanged, immediate: true));
        Track(NodeImpl.SubscribeProperty<InVisible, bool>(OnVisibleChanged, immediate: true));
        Track(NodeImpl.SubscribeAncestorProperty<ReadOnly, bool>(OnReadOnlyChanged, immediate: true));
        Track(NodeImpl.SubscribeAncestorProperty<Disable, bool>(OnDisabledChanged, immediate: true));
        Track(NodeImpl.SubscribeProperty<DisplayOnly, bool>(OnDisplayOnlyChanged, immediate: true));

        // Cached labels must follow a language switch without rebuilding the visual tree.
        _languageChangedHandler = OnLanguageChangedHandler;
        LocaleManager.Instance.LanguageChanged += _languageChangedHandler;
    }

    #region Identity

    /// <summary>Underlying Core node.</summary>
    public DataNode Node => NodeImpl;

    /// <summary>The registry this wrapper and its descendants resolve templates through.</summary>
    public SchemaViewRegistry Registry { get; }

    /// <summary>Render role (field / table cell).</summary>
    public SchemaViewRole Role { get; }

    /// <summary>Schema field name, when the node is a struct field.</summary>
    public string? FieldName { get; }

    /// <summary>Core node used by derived wrappers.</summary>
    protected DataNode NodeImpl { get; }

    /// <summary>Dispatcher used for all UI-thread hand-offs.</summary>
    protected Dispatcher Dispatcher => _dispatcher;

    /// <summary>Whether the wrapper has been disposed.</summary>
    public bool IsDisposed => _disposed;

    #endregion

    #region Bindable state

    /// <summary>
    /// Node value. The setter converts the incoming editor value; a conversion failure is
    /// surfaced through <see cref="INotifyDataErrorInfo"/> instead of silently dropping the edit.
    /// </summary>
    public object? Value
    {
        get => NodeImpl.GetValue<object>();
        set
        {
            if (_disposed) return;
            bool ok = NodeImpl.TrySetValue(value);
            SetInputError(ok
                ? null
                : LocaleManager.Instance.Format("wpf.error.value.convert", value, NodeImpl.Type.Name));
        }
    }

    /// <summary>
    /// Whether the node should be rendered. Core computes it from Visible / InVisible and from
    /// relation-derived Attach properties, so reacting to both property streams is enough.
    /// </summary>
    public bool IsVisible => NodeImpl.Visible;

    /// <summary>Whether any ancestor or the node itself marks the field read-only.</summary>
    public bool IsReadOnly => _readOnly;

    /// <summary>Whether any ancestor or the node itself disables the field.</summary>
    public bool IsDisabled => _disabled;

    /// <summary>
    /// Whether the owner of this wrapper (a table in row-editor mode, for example) forces the
    /// subtree to render read-only without writing the Core <c>ReadOnly</c> property.
    /// </summary>
    public bool IsForcedReadOnly => _forcedReadOnly;

    /// <summary>The field is meant for display only.</summary>
    public bool IsDisplayOnly => NodeImpl.DisplayOnly;

    /// <summary>Editors are enabled only when not read-only, disabled, forced read-only or display-only.</summary>
    public bool IsEditable => !_readOnly && !_disabled && !_forcedReadOnly && !NodeImpl.DisplayOnly;

    /// <summary>Display label (localized Display property, schema name or field name).</summary>
    public string Label
    {
        get
        {
            LocaleString? display = NodeImpl.GetPropertyValue<Display, LocaleString>();
            if (display is not null) return display.ToString(NodeImpl.Context);
            return NodeImpl.Name ?? FieldName ?? NodeImpl.Type.Name;
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
    /// rule (e.g. <c>VALUE_NOT_VALID</c>) when a constraint carries no message of its own, so the
    /// view must translate it and, failing that, substitute the generic localized message instead
    /// of showing a raw code.
    /// </summary>
    private static string ResolveMessage(string? message)
    {
        if (message is not null && LocaleManager.Instance.TryGet(message, out string translated))
            return translated;
        if (message is not null && !LooksLikeStatusCode(message))
            return message;
        return LocaleManager.Instance.Get("wpf.view.error.invalid");
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

    /// <summary>Whether the error should be rendered inline below the editor.</summary>
    public bool ShowInlineError => Role == SchemaViewRole.Field;

    /// <summary>Whether an inline error block should be visible.</summary>
    public bool ShowError => ShowInlineError && HasErrors;

    /// <summary>Remove action installed by the parent array/table wrapper (null when not a row).</summary>
    public ICommand? RemoveCommand { get; internal set; }

    /// <summary>
    /// Template this wrapper renders through by default. Derived wrappers override it so an
    /// unknown node kind still reaches the matching view instead of falling back to the input view.
    /// </summary>
    public virtual string? DefaultTemplateKey => null;

    /// <summary>Host-supplied template override; wins over registry lookup when set.</summary>
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

    #region INotifyDataErrorInfo

    /// <inheritdoc />
    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    /// <inheritdoc />
    public bool HasErrors => _inputError is not null || !NodeImpl.IsValid;

    /// <inheritdoc />
    public IEnumerable GetErrors(string? propertyName)
    {
        if (!string.IsNullOrEmpty(propertyName) && propertyName != nameof(Value))
            return Array.Empty<object>();

        string? error = Error;
        return error is null ? Array.Empty<object>() : new object[] { error };
    }

    #endregion

    #region Threading / subscriptions

    /// <summary>
    /// Schedules an action on the dispatcher. Never uses a synchronous Invoke, so a UI-thread
    /// wait can never deadlock against a Core continuation.
    /// </summary>
    protected void Post(Action action)
    {
        if (_disposed) return;
        _dispatcher.InvokeAsync(() =>
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
        RaiseErrorsChanged();
    }

    private void OnVisibleChanged((IValueAccess node, Type propertyType, bool value, bool oldValue) args)
    {
        if (_disposed) return;
        RaisePropertyChanged(nameof(IsVisible));
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
    internal void SetForcedReadOnly(bool value)
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
    }

    /// <summary>Sets (or clears) the value-conversion / entry-loading error shown by the view.</summary>
    protected void SetInputError(string? error)
    {
        if (string.Equals(_inputError, error, StringComparison.Ordinal)) return;
        _inputError = error;
        RaiseErrorsChanged();
    }

    private void RaiseErrorsChanged()
    {
        Post(() =>
        {
            RaisePropertyChangedNow(nameof(HasErrors));
            RaisePropertyChangedNow(nameof(ShowError));
            RaisePropertyChangedNow(nameof(Error));
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(Value)));
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
