using System.ComponentModel;
using System.Text.Json.Nodes;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Struct;
using SchemaNode.WPF.View.Localization;

namespace SchemaNode.WPF.View.NodeWrapper;

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
/// Candidate lists are loaded asynchronously from Core. Every Core notification (including
/// <c>EntrySourceVersion</c>) starts a cancellable load; results are applied on the dispatcher in
/// one bulk collection update. Nothing blocks the UI thread in the constructor.
/// </summary>
public sealed class InputWrapper : ScalarWrapper
{
    private CancellationTokenSource? _entryCts;
    private EntryItem? _selectedEntry;
    private bool _suppressSelection;
    private bool _suppressMultiWrite;
    private bool _isPopupOpen;
    private bool _isMulti;
    private bool _isCascading;

    /// <inheritdoc />
    public override string? DefaultTemplateKey => SchemaTemplateKeys.Input;

    public InputWrapper(DataNode node, SchemaViewRegistry? registry, string? fieldName, SchemaViewRole role)
        : base(node, registry, fieldName, role)
    {
        _isMulti = node is EnumArrayNode;
        Track(node.SubscribeProperty<EntrySourceVersion, long>(OnEntrySourceVersionChanged, immediate: true));
        RefreshEntries();
    }

    /// <summary>Whether the node is an enum array rendered as a multi-value popup.</summary>
    public override bool IsMulti => _isMulti;

    /// <summary>
    /// Whether the entry source has multiple cascaded levels. Core resolves the entry source
    /// asynchronously, so this value is decided when a snapshot is applied, not at construction.
    /// </summary>
    public override bool IsCascading => _isCascading;

    /// <summary>Single-level candidates.</summary>
    public BulkObservableCollection<EntryItem> Entries { get; } = new();

    /// <summary>Cascaded levels; each level is a compact drop-down.</summary>
    public BulkObservableCollection<EntryLevel> Levels { get; } = new();

    /// <summary>Multi-value candidates.</summary>
    public BulkObservableCollection<MultiEntryItem> MultiEntries { get; } = new();

    /// <summary>Whether at least one candidate is available (drives ComboBox vs TextBox).</summary>
    public bool HasEntries => IsCascading ? Levels.Count > 0 : IsMulti ? MultiEntries.Count > 0 : Entries.Count > 0;

    /// <summary>Use the single-level drop-down.</summary>
    public override bool IsDropDown => !IsMulti && !IsCascading && HasEntries;

    /// <summary>Use the plain text editor (no candidates at all).</summary>
    public override bool IsPlainText => !IsMulti && !IsCascading && !HasEntries;

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

    /// <summary>Compact closed-state summary for the multi-value popup.</summary>
    public string MultiSummary
    {
        get
        {
            int count = MultiEntries.Count(m => m.IsSelected);
            if (count == 0) return LocaleManager.Instance.Get("wpf.entry.multi.none");
            if (count <= 2)
                return string.Join(", ", MultiEntries.Where(m => m.IsSelected).Select(m => m.Display));
            return LocaleManager.Instance.Format("wpf.entry.multi.count", count);
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

    /// <inheritdoc />
    protected override void OnValueChanged()
    {
        Post(SyncSelectionFromNode);
    }

    /// <inheritdoc />
    protected override void OnLanguageChanged()
    {
        Post(() =>
        {
            RaisePropertyChangedNow(nameof(MultiSummary));
            RefreshEntries();
        });
    }

    private void OnEntrySourceVersionChanged((IValueAccess node, Type propertyType, long value, long oldValue) args)
    {
        RefreshEntries();
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

            await Dispatcher.InvokeAsync(() =>
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
            Post(() => SetInputError(LocaleManager.Instance.Format("wpf.view.error.entries", ex.Message)));
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
                for (int i = 0; i < access.Length; i++)
                {
                    string? selected = ToKey(access[i].Entry?.Value);
                    levels.Add(new LevelSnapshot(
                        (access[i].Children ?? []).Select(ToItem).ToArray(), selected));
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
        RaisePropertyChangedNow(nameof(IsPlainText));
        RaisePropertyChangedNow(nameof(MultiSummary));
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

        bool selectable = !item.HasChildren || LeafOnly || ScalarNodeImpl.IsNonLeafNodeSelectable;
        if (selectable)
            Value = item.Value;

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

            await Dispatcher.InvokeAsync(() =>
            {
                if (IsDisposed) return;

                List<EntryLevel> levels = Levels.Take(index + 1).ToList();
                if (levels.Count == 0) return;

                EntryLevel next = new(levels.Count, OnLevelSelected);
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
            });
        }
        catch (Exception ex)
        {
            Post(() => SetInputError(LocaleManager.Instance.Format("wpf.view.error.entries", ex.Message)));
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
            display?.ToString(NodeImpl.Context) ?? entry.Value,
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
            Interlocked.Exchange(ref _entryCts, null)?.Cancel();
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
