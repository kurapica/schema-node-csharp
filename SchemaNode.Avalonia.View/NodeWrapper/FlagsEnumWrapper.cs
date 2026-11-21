using System.ComponentModel;
using System.Globalization;
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

/// <summary>
/// Wraps a flags enum. The value is a bit mask, so the candidates are the entry values themselves:
/// a single-flag node renders one drop-down, a multi-flag node renders the check-box popup and
/// writes the OR of the selected bits. This mirrors flagEnumView.vue.
///
/// The flag characteristic lives on the node type, which is why this wrapper is selected by the
/// node resolver instead of by a generic enum wrapper.
/// </summary>
public sealed class FlagsEnumWrapper : DataNodeWrapper
{
    private CancellationTokenSource? _entryCts;
    private EntryItem? _selectedEntry;
    private bool _suppressSelection;
    private bool _suppressMultiWrite;
    private bool _isPopupOpen;
    private bool _isMulti;

    /// <inheritdoc />
    public override string? DefaultTemplateKey => SchemaTemplateKeys.FlagsEnum;

    public FlagsEnumWrapper(DataNode node, SchemaViewRegistry? registry, string? fieldName, SchemaViewRole role)
        : base(node, registry, fieldName, role)
    {
        EnumNodeImpl = node as EnumNode
            ?? throw new ArgumentException($"Node '{node.Type.Name}' is not an enum node.", nameof(node));

        _isMulti = EnumNodeImpl.GetProperty<SingleFlag>()?.Value != true;
        Track(node.SubscribeProperty<SingleFlag, bool>(OnSingleFlagChanged, immediate: false));
        Track(node.SubscribeProperty<EntrySourceVersion, long>(OnEntrySourceVersionChanged, immediate: true));
        RefreshEntries();
    }

    /// <summary>The typed enum node.</summary>
    public EnumNode EnumNodeImpl { get; }

    /// <summary>Whether more than one flag can be selected.</summary>
    public bool IsMulti => _isMulti;

    /// <summary>Single-flag candidates.</summary>
    public BulkObservableCollection<EntryItem> Entries { get; } = new();

    /// <summary>Multi-flag candidates.</summary>
    public BulkObservableCollection<MultiEntryItem> MultiEntries { get; } = new();

    /// <summary>Whether the multi-flag popup is open.</summary>
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

    /// <summary>Single-flag selection.</summary>
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

    /// <summary>Closed-state summary for the popup, or "none" when nothing is selected.</summary>
    public string MultiSummary
    {
        get
        {
            List<string> selected = SelectedDisplays();
            if (selected.Count == 0) return LocaleManager.Instance.Get("avalonia.entry.multi.none");
            if (selected.Count <= 2) return string.Join(", ", selected);
            return LocaleManager.Instance.Format("avalonia.entry.multi.count", selected.Count);
        }
    }

    /// <summary>Single-flag summary shown when the editor is disabled.</summary>
    public string SingleSummary
        => _selectedEntry?.Display
           ?? LocalizedDisplay.Resolve(null, NodeImpl.GetValue<object>()?.ToString(), NodeImpl.Context);

    /// <inheritdoc />
    protected override void OnValueChanged()
        => Post(SyncSelectionFromNode);

    /// <inheritdoc />
    protected override void OnLanguageChanged() => Post(RefreshEntries);

    private void OnSingleFlagChanged((IValueAccess node, Type propertyType, bool value, bool oldValue) args)
    {
        bool multi = !args.value;
        if (_isMulti == multi) return;
        _isMulti = multi;
        RaisePropertyChanged(nameof(IsMulti));
        RefreshEntries();
    }

    private void OnEntrySourceVersionChanged((IValueAccess node, Type propertyType, long value, long oldValue) args)
        => RefreshEntries();

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
            Entry<string>[] roots = await EnumNodeImpl.GetSubEntryListAsync(null, cts.Token).ConfigureAwait(false);
            if (cts.IsCancellationRequested || IsDisposed) return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (cts.IsCancellationRequested || IsDisposed) return;
                ApplyEntries(roots);
            });
        }
        catch (OperationCanceledException)
        {
            // A newer entry-source version superseded this load.
        }
        catch (Exception ex)
        {
            Post(() => SetInputError(LocaleManager.Instance.Format("avalonia.view.error.entries", ex.Message)));
        }
    }

    private void ApplyEntries(Entry<string>[] roots)
    {
        EntryItem[] items = roots.Select(ToItem).ToArray();
        long mask = ReadMask();

        _suppressSelection = true;
        try
        {
            if (_isMulti)
            {
                MultiEntries.ReplaceAll(items.Select(item =>
                    new MultiEntryItem(item.Value, item.Display, IsBitSelected(mask, item.Value), OnMultiChanged)));
            }
            else
            {
                Entries.ReplaceAll(items);
                _selectedEntry = items.FirstOrDefault(item =>
                    string.Equals(item.Value, mask.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal));
            }
        }
        finally
        {
            _suppressSelection = false;
        }

        RaisePropertyChangedNow(nameof(SelectedEntry));
        RaisePropertyChangedNow(nameof(MultiSummary));
        RaisePropertyChangedNow(nameof(SingleSummary));
    }

    private void SyncSelectionFromNode()
    {
        if (IsDisposed || _suppressSelection) return;

        long mask = ReadMask();
        _suppressSelection = true;
        try
        {
            if (_isMulti)
            {
                foreach (MultiEntryItem item in MultiEntries)
                    item.SetSelectedSilently(IsBitSelected(mask, item.Value));
            }
            else
            {
                _selectedEntry = Entries.FirstOrDefault(item =>
                    string.Equals(item.Value, mask.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal));
            }
        }
        finally
        {
            _suppressSelection = false;
        }

        RaisePropertyChangedNow(nameof(SelectedEntry));
        RaisePropertyChangedNow(nameof(MultiSummary));
        RaisePropertyChangedNow(nameof(SingleSummary));
    }

    private void OnMultiChanged(MultiEntryItem item, bool isSelected)
    {
        if (_suppressMultiWrite || IsDisposed) return;

        List<MultiEntryItem> selected = MultiEntries.Where(m => m.IsSelected).ToList();
        long mask = 0;
        foreach (MultiEntryItem entry in selected)
        {
            long bit = ParseValue(entry.Value);
            if (bit == 0)
            {
                mask = 0;
                break;
            }
            mask |= bit;
        }

        _suppressMultiWrite = true;
        try
        {
            NodeImpl.TrySetValue(mask.ToString(CultureInfo.InvariantCulture));
        }
        finally
        {
            _suppressMultiWrite = false;
        }

        RaisePropertyChangedNow(nameof(MultiSummary));
    }

    private List<string> SelectedDisplays()
        => MultiEntries.Where(m => m.IsSelected).Select(m => m.Display).ToList();

    private long ReadMask()
    {
        object? raw = NodeImpl.GetValue<object>();
        if (raw is null) return 0;
        if (raw is long l) return l;
        if (raw is int i) return i;
        try
        {
            return Convert.ToInt64(raw, CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private static long ParseValue(string value)
    {
        try
        {
            return long.Parse(value, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            return 0;
        }
        catch (OverflowException)
        {
            return 0;
        }
    }

    private static bool IsBitSelected(long mask, string value)
    {
        long bit = ParseValue(value);
        if (bit == 0) return mask == 0;
        return (mask & bit) == bit;
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
}
