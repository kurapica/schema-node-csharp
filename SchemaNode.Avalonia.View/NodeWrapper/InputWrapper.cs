using System.ComponentModel;
using System.Text.Json.Nodes;
using Avalonia.Threading;
using SchemaNode.Avalonia.View.Localization;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Enum;
using SchemaNode.Runtime;
using SchemaNode.Struct;

namespace SchemaNode.Avalonia.View.NodeWrapper;

/// <summary>A single entry candidate.</summary>
public sealed class EntryItem
{
    public EntryItem(string value, string display, bool hasChildren, bool disabled)
    {
        Value = value;
        Display = display;
        HasChildren = hasChildren;
        Disabled = disabled;
    }

    /// <summary>Raw value written into the node.</summary>
    public string Value { get; }

    /// <summary>Localized display text.</summary>
    public string Display { get; }

    /// <summary>Whether the entry has a further level.</summary>
    public bool HasChildren { get; }

    /// <summary>Whether the entry is disabled by Core.</summary>
    public bool Disabled { get; }

    /// <inheritdoc />
    public override string ToString() => Display;
}

/// <summary>One compact drop-down level of a cascaded entry source.</summary>
public sealed class EntryLevel : INotifyPropertyChanged
{
    private readonly Action<int, EntryItem?> _onSelected;
    private EntryItem? _selected;

    internal EntryLevel(int index, Action<int, EntryItem?> onSelected)
    {
        Index = index;
        _onSelected = onSelected;
    }

    /// <summary>Zero-based level index.</summary>
    public int Index { get; }

    /// <summary>Candidates of this level.</summary>
    public BulkObservableCollection<EntryItem> Entries { get; } = new();

    /// <summary>Selected candidate of this level. Selecting triggers the next level / value write.</summary>
    public EntryItem? Selected
    {
        get => _selected;
        set
        {
            if (ReferenceEquals(_selected, value)) return;
            _selected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected)));
            _onSelected(Index, value);
        }
    }

    internal void SetSelectedSilently(EntryItem? item)
    {
        if (ReferenceEquals(_selected, item)) return;
        _selected = item;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected)));
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>A candidate of the multi-value popup.</summary>
public sealed class MultiEntryItem : INotifyPropertyChanged
{
    private readonly Action<MultiEntryItem, bool> _onChanged;
    private bool _isSelected;

    internal MultiEntryItem(string value, string display, bool isSelected, Action<MultiEntryItem, bool> onChanged)
    {
        Value = value;
        Display = display;
        _isSelected = isSelected;
        _onChanged = onChanged;
    }

    /// <summary>Raw value written into the node.</summary>
    public string Value { get; }

    /// <summary>Localized display text.</summary>
    public string Display { get; }

    /// <summary>Whether this value is part of the current array value.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            _onChanged(this, value);
        }
    }

    internal void SetSelectedSilently(bool selected)
    {
        if (_isSelected == selected) return;
        _isSelected = selected;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <inheritdoc />
    public override string ToString() => Display;
}

/// <summary>
/// Entry-source wrapper (single value, cascaded levels and multi-value enum arrays).
///
/// Everything entry-related lives here: candidate lists, selection, levels and multi-value
/// summaries. Permission constraints such as <c>Require</c> are inherited from
/// <see cref="DataNodeWrapper"/> so Date and Bool do not duplicate them.
///
/// Candidate lists are loaded asynchronously from Core. Every Core notification (including
/// <c>EntrySourceVersion</c>) starts a cancellable load; results are applied on the dispatcher in
/// one bulk collection update. Nothing blocks the UI thread in the constructor.
/// </summary>
public sealed class InputWrapper : DataNodeWrapper
{
    private CancellationTokenSource? _entryCts;
    private EntryItem? _selectedEntry;
    private bool _suppressSelection;
    private bool _suppressMultiWrite;
    private bool _isPopupOpen;
    private bool _isSuggestOpen;
    private readonly bool _isMulti;
    private bool _isCascading;
    private bool _asSuggest;
    private bool _leafOnly;
    private string? _pendingText;
    private bool _applyingInput;

    /// <inheritdoc />
    public override string? DefaultTemplateKey => SchemaTemplateKeys.Input;

    public InputWrapper(DataNode node, SchemaViewRegistry? registry, string? fieldName, SchemaViewRole role)
        : base(node, registry, fieldName, role)
    {
        ScalarNodeImpl = node as ScalarNode
            ?? throw new ArgumentException($"Node '{node.Type.Name}' is not a scalar node.", nameof(node));

        _isMulti = node is EnumArrayNode;
        Track(node.SubscribeProperty<EntrySourceVersion, long>(OnEntrySourceVersionChanged, immediate: true));
        Track(node.SubscribeProperty<AsSuggest, bool>(OnAsSuggestChanged, immediate: true));
        Track(node.SubscribeProperty<LeafOnly, bool>(OnLeafOnlyChanged, immediate: true));
        RefreshEntries();
    }

    /// <summary>The typed scalar node behind the entry source.</summary>
    private ScalarNode ScalarNodeImpl { get; }

    /// <summary>Whether the drop-down allows free text input.</summary>
    public bool AsSuggest => _asSuggest;

    /// <summary>Whether only leaf entries are selectable.</summary>
    public bool LeafOnly => _leafOnly;

    /// <summary>Whether the node is an enum array rendered as a multi-value popup.</summary>
    public bool IsMulti => _isMulti;

    /// <summary>
    /// Whether the editor currently holds text that could not be committed. It applies to
    /// text-like inputs only (numbers, strings and free-text entries); booleans and pickers always
    /// produce a valid typed value and never enter this state.
    /// </summary>
    public bool HasPendingInput => _pendingText is not null;

    /// <summary>
    /// Whether the entry source has multiple cascaded levels. Core resolves the entry source
    /// asynchronously, so this value is decided when a snapshot is applied, not at construction.
    /// </summary>
    public bool IsCascading => _isCascading;

    /// <summary>Single-level candidates.</summary>
    public BulkObservableCollection<EntryItem> Entries { get; } = new();

    /// <summary>Cascaded levels; each level is a compact drop-down.</summary>
    public BulkObservableCollection<EntryLevel> Levels { get; } = new();

    /// <summary>Multi-value candidates.</summary>
    public BulkObservableCollection<MultiEntryItem> MultiEntries { get; } = new();

    /// <summary>Whether at least one candidate is available (drives ComboBox vs TextBox).</summary>
    public bool HasEntries => IsCascading ? Levels.Count > 0 : IsMulti ? MultiEntries.Count > 0 : Entries.Count > 0;

    /// <summary>Use the single-level drop-down (candidates present, free text disabled).</summary>
    public bool IsDropDown => !IsMulti && !IsCascading && HasEntries && !AsSuggest;

    /// <summary>
    /// Use the suggestion editor: free text plus a candidate list. Avalonia's combo box is not
    /// editable, so suggestion mode is a text box with its own drop-down instead of one control.
    /// </summary>
    public bool IsSuggest => !IsMulti && !IsCascading && HasEntries && AsSuggest;

    /// <summary>Use the plain text editor (no candidates at all).</summary>
    public bool IsPlainText => !IsMulti && !IsCascading && !HasEntries;

    /// <inheritdoc />
    protected override object? ReadEditorValue()
        => _pendingText ?? base.ReadEditorValue();

    /// <inheritdoc />
    protected override void WriteEditorValue(object? value)
    {
        if (IsDisposed) return;

        string text = value as string ?? value?.ToString() ?? string.Empty;

        _applyingInput = true;
        bool ok;
        try
        {
            ok = NodeImpl.TrySetValue(value);
        }
        finally
        {
            _applyingInput = false;
        }

        if (!ok)
        {
            // The node rejected the text (wrong type or failed constraint). Core owns the
            // validation semantics; the wrapper only keeps the draft and reports the generic
            // VALUE_NOT_VALID message until the next write attempt.
            _pendingText = text;
            SetInputError(LocaleManager.Instance.TryGet("VALUE_NOT_VALID", out string message)
                ? message
                : LocaleManager.Instance.Get("avalonia.view.error.invalid"));
            RaisePropertyChangedNow(nameof(HasPendingInput));
            return;
        }

        ClearPendingInput();
    }

    private void ClearPendingInput()
    {
        bool changed = _pendingText is not null;
        _pendingText = null;
        SetInputError(null);
        if (changed)
            RaisePropertyChangedNow(nameof(HasPendingInput));
    }

    /// <summary>Currently selected single-level candidate.</summary>
    public EntryItem? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (ReferenceEquals(_selectedEntry, value)) return;
            _selectedEntry = value;
            RaisePropertyChangedNow(nameof(SelectedEntry));
            if (!_suppressSelection && value is not null)
                Value = value.Value;
        }
    }

    /// <summary>
    /// Closed-state text of the cascaded selector: the display of every selected level joined by
    /// <c> / </c>, root first. When an existing value no longer resolves against the current entry
    /// tree the raw value is shown instead.
    /// </summary>
    public string CascadeSummary
    {
        get
        {
            List<string> parts = [];
            foreach (EntryLevel level in Levels)
            {
                if (level.Selected is { } selected)
                    parts.Add(selected.Display);
            }
            if (parts.Count > 0) return string.Join(" / ", parts);

            string? raw = ToKey(NodeImpl.GetValue<object>());
            if (!string.IsNullOrWhiteSpace(raw))
                return LocalizedDisplay.Resolve(null, raw, NodeImpl.Context);
            return LocaleManager.Instance.Get("avalonia.entry.multi.none");
        }
    }

    /// <summary>Whether the node currently holds a value the cascader can clear.</summary>
    public bool HasCascadeValue => HasValue;

    /// <summary>Whether the node currently holds any committed value, in any editor mode.</summary>
    public bool HasValue
    {
        get
        {
            if (NodeImpl.IsEmpty) return false;
            object? raw = NodeImpl.GetValue<object>();
            return raw is JsonArray array ? array.Count > 0 : !string.IsNullOrWhiteSpace(ToKey(raw));
        }
    }

    /// <summary>
    /// Whether the committed value can be cleared. Only optional, editable selectors expose the
    /// clear affordance; required selectors keep their value and surface the usual validation.
    /// </summary>
    public bool CanClear => IsEditable && !Require && HasValue;

    /// <summary>Whether entries with children are selectable (non-leaf selection is enabled).</summary>
    public bool CanSelectNonLeaf => !LeafOnly && ScalarNodeImpl.IsNonLeafNodeSelectable;

    /// <summary>Compact closed-state summary for the multi-value popup.</summary>
    public string MultiSummary
    {
        get
        {
            int count = MultiEntries.Count(m => m.IsSelected);
            if (count == 0) return LocaleManager.Instance.Get("avalonia.entry.multi.none");
            if (count <= 2)
                return string.Join(", ", MultiEntries.Where(m => m.IsSelected).Select(m => m.Display));
            return LocaleManager.Instance.Format("avalonia.entry.multi.count", count);
        }
    }

    /// <summary>Opens/closes the multi-value popup.</summary>
    public bool IsPopupOpen
    {
        get => _isPopupOpen;
        set
        {
            if (_isPopupOpen == value) return;
            _isPopupOpen = value;
            RaisePropertyChangedNow(nameof(IsPopupOpen));
        }
    }

    /// <summary>Opens/closes the suggestion drop-down.</summary>
    public bool IsSuggestOpen
    {
        get => _isSuggestOpen;
        set
        {
            if (_isSuggestOpen == value) return;
            _isSuggestOpen = value;
            RaisePropertyChangedNow(nameof(IsSuggestOpen));
        }
    }

    /// <inheritdoc />
    protected override void OnValueChanged()
    {
        // A notification outside our own write means another writer replaced the value (a relation,
        // a reset, a data-source switch). The pending text is no longer authoritative, so drop it
        // and let the new node value reach the editor.
        if (!_applyingInput && _pendingText is not null)
        {
            _pendingText = null;
            SetInputError(null);
        }

        Post(() =>
        {
            SyncSelectionFromNode();
            RaisePropertyChangedNow(nameof(HasCascadeValue));
            RaisePropertyChangedNow(nameof(HasValue));
            RaisePropertyChangedNow(nameof(CanClear));
            RaisePropertyChangedNow(nameof(CascadeSummary));
            RaisePropertyChangedNow(nameof(HasPendingInput));
        });
    }

    /// <inheritdoc />
    protected override void OnEditableChanged()
    {
        base.OnEditableChanged();
        RaisePropertyChangedNow(nameof(CanClear));
    }

    /// <inheritdoc />
    protected override void OnRequireChanged((IValueAccess node, Type propertyType, bool value, bool oldValue) args)
    {
        base.OnRequireChanged(args);
        RaisePropertyChangedNow(nameof(CanClear));
    }

    /// <inheritdoc />
    protected override void OnLanguageChanged()
    {
        Post(() =>
        {
            RaisePropertyChangedNow(nameof(MultiSummary));
            RaisePropertyChangedNow(nameof(CascadeSummary));
            RefreshEntries();
        });
    }

    /// <summary>
    /// Reloads the level tree from the node's current value. The cascader calls this when a popup
    /// closes without a leaf commit, so in-progress parent selections are discarded.
    /// </summary>
    public void ReloadCascadeSelection() => RefreshEntries();

    /// <summary>
    /// Clears the committed value of any optional selector (single, multi or cascaded) and returns
    /// the editor to the unselected state. Required selectors keep their value because they are
    /// not clearable.
    /// </summary>
    public void ClearSelection()
    {
        if (IsDisposed || !CanClear) return;

        ClearPendingInput();
        NodeImpl.ClearValue();
        _selectedEntry = null;
        IsPopupOpen = false;
        RaisePropertyChangedNow(nameof(SelectedEntry));
        RaisePropertyChangedNow(nameof(HasCascadeValue));
        RaisePropertyChangedNow(nameof(HasValue));
        RaisePropertyChangedNow(nameof(CanClear));
        RaisePropertyChangedNow(nameof(CascadeSummary));
        RaisePropertyChangedNow(nameof(MultiSummary));
        RefreshEntries();
    }

    /// <summary>Kept for the cascader's clear affordance; delegates to the generic clear.</summary>
    public void ClearCascade() => ClearSelection();

    private void OnEntrySourceVersionChanged((IValueAccess node, Type propertyType, long value, long oldValue) args)
    {
        RefreshEntries();
    }

    private void OnAsSuggestChanged((IValueAccess node, Type propertyType, bool value, bool oldValue) args)
    {
        if (_asSuggest == args.value) return;
        _asSuggest = args.value;
        RaisePropertyChanged(nameof(AsSuggest));
        RaisePropertyChanged(nameof(IsDropDown));
        RaisePropertyChanged(nameof(IsSuggest));
        RaisePropertyChanged(nameof(IsPlainText));
    }

    private void OnLeafOnlyChanged((IValueAccess node, Type propertyType, bool value, bool oldValue) args)
    {
        if (_leafOnly == args.value) return;
        _leafOnly = args.value;
        RaisePropertyChanged(nameof(LeafOnly));
        RaisePropertyChanged(nameof(CanSelectNonLeaf));
    }

    private void RefreshEntries()
    {
        if (IsDisposed) return;

        CancellationTokenSource cts = new();
        Interlocked.Exchange(ref _entryCts, cts)?.Cancel();
        _ = RefreshEntriesAsync(cts);
    }

    private async Task RefreshEntriesAsync(CancellationTokenSource cts)
    {
        try
        {
            EntrySnapshot snapshot = await LoadSnapshotAsync(cts.Token).ConfigureAwait(false);
            if (cts.IsCancellationRequested || IsDisposed) return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (cts.IsCancellationRequested || IsDisposed) return;
                ApplySnapshot(snapshot);
            });
        }
        catch (OperationCanceledException)
        {
            // A newer EntrySourceVersion superseded this load.
        }
        catch (Exception ex)
        {
            Post(() => SetInputError(LocaleManager.Instance.Format("avalonia.view.error.entries", ex.Message)));
        }
    }

    private async Task<EntrySnapshot> LoadSnapshotAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        if (IsMulti)
        {
            Entry<string>[] roots = await ScalarNodeImpl.GetSubEntryListAsync(null, token).ConfigureAwait(false);
            return EntrySnapshot.ForMulti(roots.Select(ToItem).ToArray(), ReadMultiKeys());
        }

        object? raw = NodeImpl.GetValue<object>();

        bool cascading = ScalarNodeImpl.HasEntrySource && !ScalarNodeImpl.IsSingleLevel;
        if (cascading)
        {
            EntryAccess<string>[] access = await ScalarNodeImpl
                .GetEntryAccessListAsync(raw, cancellationToken: token).ConfigureAwait(false);

            List<LevelSnapshot> levels = [];
            if (access.Length == 0)
            {
                Entry<string>[] roots = await ScalarNodeImpl
                    .GetSubEntryListAsync(null, token).ConfigureAwait(false);
                levels.Add(new LevelSnapshot(roots.Select(ToItem).ToArray(), null));
            }
            else
            {
                // The two Core access shapes offset Entry differently, so resolve the selected path
                // from the node's own value against each level's candidates. Cascaded entry values
                // are dotted full paths, so an ancestor is either the key itself or a dotted prefix.
                string? keyText = ToKey(raw);
                if (string.IsNullOrEmpty(keyText)) keyText = null;
                for (int i = 0; i < access.Length; i++)
                {
                    EntryItem[] items = (access[i].Children ?? []).Select(ToItem).ToArray();
                    string? selected = null;
                    if (keyText is not null)
                    {
                        selected = items.FirstOrDefault(x => string.Equals(x.Value, keyText, StringComparison.Ordinal))?.Value;
                        if (selected is not null)
                        {
                            keyText = null; // the key is consumed at this level
                        }
                        else
                        {
                            selected = items
                                .Where(x => keyText.StartsWith(x.Value + ".", StringComparison.Ordinal))
                                .OrderByDescending(x => x.Value.Length)
                                .Select(x => x.Value)
                                .FirstOrDefault();
                        }
                    }
                    levels.Add(new LevelSnapshot(items, selected));
                }
            }
            return EntrySnapshot.ForCascade(levels);
        }

        Entry<string>[] candidates = await ScalarNodeImpl
            .GetSubEntryListAsync(null, token).ConfigureAwait(false);
        return EntrySnapshot.ForSingle(candidates.Select(ToItem).ToArray(), ToKey(raw));
    }

    private void ApplySnapshot(EntrySnapshot snapshot)
    {
        bool cascading = snapshot.Levels is not null;
        bool changed = _isCascading != cascading;
        _isCascading = cascading;

        _suppressSelection = true;
        try
        {
            if (snapshot.Multi is not null)
            {
                MultiEntries.ReplaceAll(snapshot.Multi.Select(i =>
                    new MultiEntryItem(i.Value, i.Display, snapshot.SelectedKeys.Contains(i.Value), OnMultiChanged)));
            }
            else if (snapshot.Levels is not null)
            {
                List<EntryLevel> levels = [];
                for (int i = 0; i < snapshot.Levels.Length; i++)
                {
                    LevelSnapshot levelSnapshot = snapshot.Levels[i];
                    EntryLevel level = new(i, OnLevelSelected);
                    level.PropertyChanged += OnLevelPropertyChanged;
                    level.Entries.ReplaceAll(levelSnapshot.Items);
                    level.SetSelectedSilently(
                        levelSnapshot.Selected is null
                            ? null
                            : levelSnapshot.Items.FirstOrDefault(x => string.Equals(x.Value, levelSnapshot.Selected, StringComparison.Ordinal)));
                    levels.Add(level);
                }
                Levels.ReplaceAll(levels);
            }
            else if (snapshot.Single is not null)
            {
                Entries.ReplaceAll(snapshot.Single);
                _selectedEntry = snapshot.Selected is null
                    ? null
                    : snapshot.Single.FirstOrDefault(x => string.Equals(x.Value, snapshot.Selected, StringComparison.Ordinal));
            }
        }
        finally
        {
            _suppressSelection = false;
        }

        if (changed) RaisePropertyChangedNow(nameof(IsCascading));
        RaisePropertyChangedNow(nameof(SelectedEntry));
        RaisePropertyChangedNow(nameof(HasEntries));
        RaisePropertyChangedNow(nameof(IsDropDown));
        RaisePropertyChangedNow(nameof(IsSuggest));
        RaisePropertyChangedNow(nameof(IsPlainText));
        RaisePropertyChangedNow(nameof(MultiSummary));
        RaisePropertyChangedNow(nameof(CascadeSummary));
        RaisePropertyChangedNow(nameof(HasCascadeValue));
    }

    private void OnLevelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(EntryLevel.Selected), StringComparison.Ordinal)) return;
        RaisePropertyChangedNow(nameof(CascadeSummary));
    }

    private void SyncSelectionFromNode()
    {
        if (IsDisposed || _suppressSelection) return;

        _suppressSelection = true;
        try
        {
            if (IsMulti)
            {
                HashSet<string> selected = ReadMultiKeys();
                foreach (MultiEntryItem item in MultiEntries)
                {
                    bool isSelected = selected.Contains(item.Value);
                    if (item.IsSelected == isSelected) continue;
                    // Bypass the callback: Core already holds this value.
                    item.SetSelectedSilently(isSelected);
                }
                RaisePropertyChangedNow(nameof(MultiSummary));
                return;
            }

            if (IsCascading)
            {
                string? key = ToKey(NodeImpl.GetValue<object>());

                // The value was replaced from outside (a host restore, a relation, another editor):
                // the level selections no longer describe it, so rebuild the tree.
                bool resolved = key is null
                    ? Levels.All(level => level.Selected is null)
                    : Levels.Any(level => level.Selected is { } selected &&
                                          string.Equals(selected.Value, key, StringComparison.Ordinal));
                if (!resolved)
                {
                    RefreshEntries();
                    return;
                }

                EntryLevel? last = Levels.LastOrDefault();
                if (last is not null)
                {
                    last.SetSelectedSilently(
                        key is null ? null : last.Entries.FirstOrDefault(x => string.Equals(x.Value, key, StringComparison.Ordinal)));
                }
                return;
            }

            string? current = ToKey(NodeImpl.GetValue<object>());
            _selectedEntry = current is null
                ? null
                : Entries.FirstOrDefault(x => string.Equals(x.Value, current, StringComparison.Ordinal));
            RaisePropertyChangedNow(nameof(SelectedEntry));
        }
        finally
        {
            _suppressSelection = false;
        }
    }

    private void OnLevelSelected(int index, EntryItem? item)
    {
        if (_suppressSelection || IsDisposed || item is null) return;

        bool selectable = !item.HasChildren || (!LeafOnly && ScalarNodeImpl.IsNonLeafNodeSelectable);
        if (selectable)
        {
            Value = item.Value;
            _selectedEntry = item;
            RaisePropertyChangedNow(nameof(SelectedEntry));
        }

        RaisePropertyChangedNow(nameof(CascadeSummary));

        if (item.HasChildren)
            _ = LoadNextLevelAsync(index, item);
        else
            TruncateLevels(index + 1);
    }

    private async Task LoadNextLevelAsync(int index, EntryItem parent)
    {
        try
        {
            Entry<string>[] children = await ScalarNodeImpl
                .GetSubEntryListAsync(parent.Value).ConfigureAwait(false);
            if (IsDisposed) return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (IsDisposed) return;

                List<EntryLevel> levels = Levels.Take(index + 1).ToList();
                if (levels.Count == 0) return;

                EntryLevel next = new(levels.Count, OnLevelSelected);
                next.PropertyChanged += OnLevelPropertyChanged;
                next.Entries.ReplaceAll(children.Select(ToItem));
                _suppressSelection = true;
                try
                {
                    Levels.ReplaceAll(levels.Append(next));
                }
                finally
                {
                    _suppressSelection = false;
                }
                RaisePropertyChangedNow(nameof(HasEntries));
                RaisePropertyChangedNow(nameof(CascadeSummary));
                RaisePropertyChangedNow(nameof(HasCascadeValue));
            });
        }
        catch (Exception ex)
        {
            Post(() => SetInputError(LocaleManager.Instance.Format("avalonia.view.error.entries", ex.Message)));
        }
    }

    private void TruncateLevels(int count)
    {
        if (Levels.Count <= count) return;
        _suppressSelection = true;
        try
        {
            Levels.ReplaceAll(Levels.Take(count).ToList());
        }
        finally
        {
            _suppressSelection = false;
        }
        RaisePropertyChangedNow(nameof(CascadeSummary));
    }

    private void OnMultiChanged(MultiEntryItem item, bool isSelected)
    {
        if (_suppressMultiWrite || IsDisposed) return;

        List<string> keys = MultiEntries.Where(m => m.IsSelected).Select(m => m.Value).ToList();
        _suppressMultiWrite = true;
        try
        {
            if (NodeImpl is EnumArrayNode { EnumType.Type: EnumValueType.String })
            {
                NodeImpl.TrySetValue(keys.ToArray());
            }
            else
            {
                long[] values = keys.Select(k => long.TryParse(k, out long n) ? n : 0L).ToArray();
                NodeImpl.TrySetValue(values);
            }
        }
        finally
        {
            _suppressMultiWrite = false;
        }

        RaisePropertyChangedNow(nameof(MultiSummary));
    }

    private HashSet<string> ReadMultiKeys()
    {
        JsonArray? array = NodeImpl.GetValue<JsonArray>();
        return array is null
            ? []
            : array.Select(ToKey).Where(k => k is not null).Select(k => k!).ToHashSet(StringComparer.Ordinal);
    }

    private EntryItem ToItem(Entry<string> entry)
    {
        LocaleString? display = entry.GetProperty<Display>()?.Value;
        return new EntryItem(
            entry.Value,
            LocalizedDisplay.Resolve(display, entry.Value, NodeImpl.Context),
            entry.HasChildren == true,
            entry.Disabled);
    }

    private static string? ToKey(object? value)
    {
        return value switch
        {
            null => null,
            string text => text,
            JsonValue json when json.TryGetValue<string>(out var text) => text,
            _ => value.ToString()
        };
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !IsDisposed)
        {
            Interlocked.Exchange(ref _entryCts, null)?.Cancel();
            _pendingText = null;
        }
        base.Dispose(disposing);
    }

    private sealed record LevelSnapshot(EntryItem[] Items, string? Selected);

    private sealed class EntrySnapshot
    {
        private EntrySnapshot()
        {
        }

        public EntryItem[]? Single { get; private init; }

        public string? Selected { get; private init; }

        public LevelSnapshot[]? Levels { get; private init; }

        public EntryItem[]? Multi { get; private init; }

        public HashSet<string> SelectedKeys { get; private init; } = [];

        public static EntrySnapshot ForSingle(EntryItem[] items, string? selected)
            => new() { Single = items, Selected = selected };

        public static EntrySnapshot ForCascade(List<LevelSnapshot> levels)
            => new() { Levels = levels.ToArray() };

        public static EntrySnapshot ForMulti(EntryItem[] items, HashSet<string> selected)
            => new() { Multi = items, SelectedKeys = selected };
    }
}
