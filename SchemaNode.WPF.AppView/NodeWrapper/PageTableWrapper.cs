using System.Text.Json.Nodes;
using System.Windows.Input;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Property.App;
using SchemaNode.Property.Common;
using SchemaNode.Runtime;
using SchemaNode.Utility;
using SchemaNode.WPF.AppView;
using SchemaNode.WPF.View;
using SchemaNode.WPF.View.Localization;
using SchemaNode.WPF.View.NodeWrapper;

namespace SchemaNode.WPF.AppView.NodeWrapper;

/// <summary>
/// A pageable struct table. It derives from <see cref="TableWrapper"/> so column derivation,
/// two-level headers, rowspan merging, sub-tables, inline cell editing and the row editor are the
/// same implementation as the plain table. The page-specific parts are additive:
///
///   * page state (skip/take/total) comes from <see cref="IPageTableAdapter"/>, never from Core;
///   * the filter bar is built from the field's <c>Filters</c> property;
///   * deletion is recorded in a primary-key tracker and restored on request instead of removing
///     the element, mirroring vue's PageNode tracker.
/// </summary>
public sealed class PageTableWrapper : TableWrapper
{
    private readonly IPageTableAdapter _adapter;
    private readonly IReadOnlyList<PageFilter> _filters;
    private readonly Dictionary<string, RowTrack> _tracker = new(StringComparer.Ordinal);
    private readonly HashSet<string> _deletedKeys = new(StringComparer.Ordinal);
    private readonly RelayCommand _firstCommand;
    private readonly RelayCommand _previousCommand;
    private readonly RelayCommand _nextCommand;
    private readonly RelayCommand _lastCommand;
    private CancellationTokenSource? _loadCts;
    private IReadOnlyList<PageTableFilterValue> _activeFilters = [];
    private PageTablePage? _currentPage;
    private int _loadVersion;
    private int _page;
    private int _total;
    private bool _isLoading;
    private bool _loadedOnce;

    public PageTableWrapper(
        DataNode node,
        SchemaViewRegistry registry,
        IPageTableAdapter adapter,
        IReadOnlyList<FieldFilter>? filters = null)
        : base(node, registry, null, SchemaViewRole.Field)
    {
        _adapter = adapter;
        _filters = BuildFilters(filters ?? []);

        _firstCommand = new RelayCommand(() => _ = GoToPageAsync(0), () => CanGoFirst);
        _previousCommand = new RelayCommand(() => _ = GoToPageAsync(Page - 1), () => CanGoPrevious);
        _nextCommand = new RelayCommand(() => _ = GoToPageAsync(Page + 1), () => CanGoNext);
        _lastCommand = new RelayCommand(() => _ = GoToPageAsync(PageCount - 1), () => CanGoLast);

        QueryCommand = new RelayCommand(ApplyFilters, () => !IsLoading);
        ResetCommand = new RelayCommand(ResetFilters, () => !IsLoading);
        ReloadCommand = new RelayCommand(() => _ = GoToPageAsync(Page), () => !IsLoading);
    }

    #region Page state

    /// <summary>Zero-based current page.</summary>
    public int Page => _page;

    /// <summary>Total rows matching the active filters.</summary>
    public int Total => _total;

    /// <summary>Number of pages; zero when the result is empty.</summary>
    public int PageCount => ComputePageCount(_total);

    /// <summary>One-based page number used by the page info text.</summary>
    public int DisplayPage => PageCount == 0 ? 0 : Math.Min(_page + 1, PageCount);

    /// <summary>Page size requested from the host.</summary>
    public int PageSize => Math.Max(1, _adapter.PageSize);

    /// <summary>Localized page position and total count.</summary>
    public string PageInfo => Total <= 0
        ? LocaleManager.Instance.Get("wpf.page.noData")
        : LocaleManager.Instance.Format("wpf.page.info", DisplayPage, PageCount, Total);

    /// <summary>Whether the page control strip should be visible.</summary>
    public bool HasPager => PageCount > 0;

    /// <summary>Whether the host explicitly supports primary-key tracking.</summary>
    public bool CanTrack =>
        _adapter.SupportsTracking &&
        ArrayNodeImpl.Type is ArrayType { Primary.Count: > 0 };

    /// <summary>Explicit degradation text shown when delete/restore is not available.</summary>
    public string? TrackingHint => CanTrack
        ? null
        : LocaleManager.Instance.Get("wpf.page.trackingUnavailable");

    /// <summary>Whether the degradation hint should be rendered.</summary>
    public bool HasTrackingHint => TrackingHint is not null;

    /// <summary>Number of rows currently marked for deletion on the visible page.</summary>
    public int DeletedRowCount => _deletedKeys.Count;

    /// <summary>Whether the tracker holds updates or deletions that the host has not accepted yet.</summary>
    public bool HasPendingChanges => _tracker.Values.Any(v => v.Delete || v.Update is not null);

    /// <summary>Edits keyed by primary key, ready for a host save call.</summary>
    public IReadOnlyList<JsonNode> PendingUpdates => _tracker.Values
        .Where(v => !v.Delete && v.Update is not null)
        .Select(v => v.Update!)
        .ToArray();

    /// <summary>Primary keys marked for deletion, ready for a host delete call.</summary>
    public IReadOnlyList<string> DeletedKeys => _deletedKeys.ToArray();

    public bool CanGoFirst => !IsLoading && Page > 0;
    public bool CanGoPrevious => !IsLoading && Page > 0;
    public bool CanGoNext => !IsLoading && Page + 1 < PageCount;
    public bool CanGoLast => !IsLoading && Page + 1 < PageCount;

    public bool IsLoading => _isLoading;

    public string LoadingText => LocaleManager.Instance.Get("wpf.page.loading");

    public string FirstText => LocaleManager.Instance.Get("wpf.page.first");
    public string PreviousText => LocaleManager.Instance.Get("wpf.page.previous");
    public string NextText => LocaleManager.Instance.Get("wpf.page.next");
    public string LastText => LocaleManager.Instance.Get("wpf.page.last");
    public string QueryText => LocaleManager.Instance.Get("wpf.page.query");
    public string ResetText => LocaleManager.Instance.Get("wpf.page.reset");

    /// <summary>Filter bar entries derived from the field's <c>Filters</c> property.</summary>
    public IReadOnlyList<PageFilter> Filters => _filters;

    public bool HasFilters => _filters.Count > 0;

    public ICommand QueryCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand ReloadCommand { get; }
    public ICommand FirstCommand => _firstCommand;
    public ICommand PreviousCommand => _previousCommand;
    public ICommand NextCommand => _nextCommand;
    public ICommand LastCommand => _lastCommand;

    #endregion

    #region Loading

    /// <summary>
    /// Loads the first page once. Called by the view when it becomes visible; repeated calls are
    /// no-ops until <see cref="Deactivate"/> releases the page.
    /// </summary>
    public async Task EnsureLoadedAsync()
    {
        if (IsDisposed || _loadedOnce) return;

        _loadedOnce = true;
        await LoadPageAsync(0, _activeFilters).ConfigureAwait(false);
    }

    /// <summary>
    /// Cancels any in-flight load and releases the current page when the surface leaves the tree.
    /// The next activation reloads page zero, so no page data is retained off-screen.
    /// </summary>
    public void Deactivate()
    {
        CancelPendingLoads();
        ReleaseCurrentPage();
        _loadedOnce = false;
    }

    private async Task LoadPageAsync(int page, IReadOnlyList<PageTableFilterValue> filters)
    {
        if (IsDisposed) return;

        if (!_adapter.SupportsFunctionFilters && filters.Any(f => f.Mode == FieldFilterMode.Filter))
        {
            RunOnUiThread(() =>
            {
                SetInputError(LocaleManager.Instance.Get("wpf.page.filterUnsupported"));
                RaisePageState();
            });
            return;
        }

        CancellationTokenSource cts = new();
        CancellationTokenSource? previous = Interlocked.Exchange(ref _loadCts, cts);
        if (previous is not null)
        {
            previous.Cancel();
            previous.Dispose();
        }

        int version = ++_loadVersion;
        RunOnUiThread(() => SetLoading(true));

        int effectivePage = Math.Max(0, page);
        PageTableQuery query = new(effectivePage * PageSize, PageSize, filters);

        try
        {
            PageTablePage result = await _adapter.LoadPageAsync(query, cts.Token).ConfigureAwait(false);

            if (version != _loadVersion || IsDisposed)
            {
                // The user navigated away while the host was still answering: release the stale page.
                _adapter.ReleasePage(result);
                return;
            }

            RunOnUiThread(() => ApplyPage(effectivePage, result, filters));
        }
        catch (OperationCanceledException)
        {
            // Expected when a newer page request superseded this one.
        }
        catch (Exception ex)
        {
            if (version == _loadVersion && !IsDisposed)
                RunOnUiThread(() =>
                {
                    SetInputError(ex.Message);
                    RaisePageState();
                });
        }
        finally
        {
            if (version == _loadVersion)
            {
                RunOnUiThread(() => SetLoading(false));
                if (ReferenceEquals(_loadCts, cts)) _loadCts = null;
                cts.Dispose();
            }
        }
    }

    private void ApplyPage(int requestedPage, PageTablePage page, IReadOnlyList<PageTableFilterValue> filters)
    {
        if (IsDisposed)
        {
            _adapter.ReleasePage(page);
            return;
        }

        _activeFilters = filters;
        SetInputError(null);

        // Snapshot edits made on the page that is being replaced, keyed by primary key.
        CaptureTracker();

        ApplyRows(page.Rows);
        ApplyTrackerToRows();

        // Loading a page is not an edit. Confirm sets Value as the new baseline so the next
        // CaptureTracker only records what the user actually changed on this page.
        ConfirmRows();

        _total = page.Total;
        int pageCount = PageCount;
        _page = pageCount == 0 ? 0 : Math.Min(Math.Max(requestedPage, 0), pageCount - 1);

        PageTablePage? previous = _currentPage;
        _currentPage = page;
        if (previous is not null) _adapter.ReleasePage(previous);

        RebuildAll();
        RaisePageState();
    }

    /// <summary>
    /// Replaces the visible elements while reusing the existing nodes row by row. Reusing keeps the
    /// cell wrappers (and local edits not yet tracked) alive across a refresh, like vue's PageNode
    /// setPage.
    /// </summary>
    private void ApplyRows(IReadOnlyList<JsonNode?> rows)
    {
        RunBulkUpdate(() =>
        {
            int existing = ArrayNodeImpl.Count;

            for (int i = 0; i < rows.Count; i++)
            {
                JsonNode? row = rows[i];
                DataNode? element = i < existing ? ArrayNodeImpl.ElementAt(i) as DataNode : null;

                if (element is not null)
                {
                    if (row is null) element.ClearValue();
                    else element.TrySetValue(row);
                    continue;
                }

                IValueAccess? created = ArrayNodeImpl.ElementType.Create(NodeImpl.Context, row, ArrayNodeImpl);
                if (created is not null) ArrayNodeImpl.AddRow(null, created);
            }

            if (existing > rows.Count)
            {
                IValueAccess[] removed = ArrayNodeImpl.Skip(rows.Count).ToArray();
                ArrayNodeImpl.DelRows(rows.Count, existing - rows.Count);
                foreach (IValueAccess node in removed) (node as IDisposable)?.Dispose();
            }
        });
    }

    /// <summary>Makes the loaded page the edit baseline for every visible element.</summary>
    private void ConfirmRows()
    {
        foreach (DataNode element in ArrayNodeImpl.OfType<DataNode>())
            element.Confirm();
    }

    /// <summary>Records the edits of the current page into the primary-key tracker.</summary>
    private void CaptureTracker()
    {
        foreach (DataNode element in ArrayNodeImpl.OfType<DataNode>())
        {
            string? key = KeyOf(element);
            if (key is null) continue;

            bool deleted = _tracker.TryGetValue(key, out RowTrack? track) && track.Delete;

            if (element.Changed && element.IsValid)
            {
                JsonNode? update = element.GetValue<JsonNode>();
                if (update is not null)
                    _tracker[key] = new RowTrack { Update = update, Delete = deleted };
            }
            else if (!deleted)
            {
                _tracker.Remove(key);
            }
        }
    }

    /// <summary>Re-applies tracked edits and delete marks to the freshly loaded page.</summary>
    private void ApplyTrackerToRows()
    {
        _deletedKeys.Clear();

        foreach (DataNode element in ArrayNodeImpl.OfType<DataNode>())
        {
            string? key = KeyOf(element);
            if (key is null || !_tracker.TryGetValue(key, out RowTrack? track)) continue;

            if (track.Delete)
            {
                _deletedKeys.Add(key);
            }
            else if (track.Update is not null)
            {
                element.TrySetValue(track.Update);
            }
        }
    }

    private void ReleaseCurrentPage()
    {
        PageTablePage? page = _currentPage;
        _currentPage = null;
        if (page is not null) _adapter.ReleasePage(page);
    }

    private void CancelPendingLoads()
    {
        _loadVersion++;
        CancellationTokenSource? cts = Interlocked.Exchange(ref _loadCts, null);
        if (cts is not null)
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    private void SetLoading(bool value)
    {
        if (_isLoading == value) return;
        _isLoading = value;
        RaisePropertyChanged(nameof(IsLoading));
        RaisePageState();
    }

    private void RaisePageState()
    {
        RaisePropertyChanged(nameof(Page));
        RaisePropertyChanged(nameof(Total));
        RaisePropertyChanged(nameof(PageCount));
        RaisePropertyChanged(nameof(DisplayPage));
        RaisePropertyChanged(nameof(PageInfo));
        RaisePropertyChanged(nameof(HasPager));
        RaisePropertyChanged(nameof(CanTrack));
        RaisePropertyChanged(nameof(TrackingHint));
        RaisePropertyChanged(nameof(HasTrackingHint));
        RaisePropertyChanged(nameof(DeletedRowCount));
        RaisePropertyChanged(nameof(HasPendingChanges));
        RaisePropertyChanged(nameof(CanGoFirst));
        RaisePropertyChanged(nameof(CanGoPrevious));
        RaisePropertyChanged(nameof(CanGoNext));
        RaisePropertyChanged(nameof(CanGoLast));

        _firstCommand.RaiseCanExecuteChanged();
        _previousCommand.RaiseCanExecuteChanged();
        _nextCommand.RaiseCanExecuteChanged();
        _lastCommand.RaiseCanExecuteChanged();
        ((RelayCommand)QueryCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ResetCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ReloadCommand).RaiseCanExecuteChanged();
    }

    #endregion

    #region Filters

    private IReadOnlyList<PageFilter> BuildFilters(IReadOnlyList<FieldFilter> filters)
    {
        if (filters.Count == 0) return [];

        List<PageFilter> result = new(filters.Count);
        foreach (FieldFilter filter in filters)
        {
            PageFilter? pageFilter = filter.Mode == FieldFilterMode.Filter
                ? BuildFunctionFilter(filter)
                : BuildValueFilter(filter);

            if (pageFilter is not null) result.Add(pageFilter);
        }

        return result;
    }

    private PageFilter BuildValueFilter(FieldFilter config)
    {
        StructFieldType? field = ElementType.GetField(config.Filter);
        List<DataNodeWrapper> inputs = [];

        if (field is not null)
        {
            IValueAccess? input = field.Type?.Create(NodeImpl.Context, null, ArrayNodeImpl);
            DataNodeWrapper? wrapper = input is null
                ? null
                : NodeWrapperFactory.Create(input, Registry, config.Filter, SchemaViewRole.Field);
            if (wrapper is not null) inputs.Add(wrapper);
        }

        string label = field is null ? config.Filter : FormatHeader(field);
        return new PageFilter(config, label, inputs);
    }

    private PageFilter? BuildFunctionFilter(FieldFilter config)
    {
        FunctionType? function = config.FilterFunction;
        if (function is null) return null;

        List<DataNodeWrapper> inputs = [];
        foreach (FunctionNodeArgument argument in function.Args.Skip(1))
        {
            IValueTypeAccess? type = argument.ValueType;
            if (type is null) continue;

            IValueAccess? input = type.Create(NodeImpl.Context, null, ArrayNodeImpl);
            DataNodeWrapper? wrapper = input is null
                ? null
                : NodeWrapperFactory.Create(input, Registry, argument.Name, SchemaViewRole.Field);
            if (wrapper is not null) inputs.Add(wrapper);
        }

        string label = function.GetProperty<Display>()?.Value?.ToString(NodeImpl.Context)
            ?? function.Name;

        return new PageFilter(config, label, inputs);
    }

    private void ApplyFilters()
    {
        _ = LoadPageAsync(0, CollectFilters());
    }

    private void ResetFilters()
    {
        foreach (PageFilter filter in _filters)
            foreach (DataNodeWrapper input in filter.Inputs)
                input.Node.ClearValue();

        _activeFilters = [];
        _ = LoadPageAsync(0, _activeFilters);
    }

    private IReadOnlyList<PageTableFilterValue> CollectFilters()
    {
        List<PageTableFilterValue> result = [];
        foreach (PageFilter filter in _filters)
        {
            if (!filter.TryReadValues(out IReadOnlyList<object?> values)) continue;
            result.Add(new PageTableFilterValue(filter.Config.Filter, filter.Config.Mode, values));
        }
        return result;
    }

    #endregion

    #region Tracker operations

    /// <summary>Page-table deletion is a pending host operation, not an element removal.</summary>
    protected override void DeleteRow(DataNode element)
    {
        if (!CanTrack || IsDisposed) return;

        string? key = KeyOf(element);
        if (key is null) return;

        RowTrack track = _tracker.TryGetValue(key, out RowTrack? existing)
            ? existing ?? new RowTrack()
            : new RowTrack();
        track.Delete = true;
        _tracker[key] = track;
        _deletedKeys.Add(key);

        RebuildAll();
        RaisePageState();
    }

    /// <summary>Clears the pending delete mark for one row.</summary>
    public void ResumeRow(DataNode element)
    {
        if (IsDisposed) return;

        string? key = KeyOf(element);
        if (key is null) return;

        if (_tracker.TryGetValue(key, out RowTrack? track)) track.Delete = false;
        _deletedKeys.Remove(key);

        RebuildAll();
        RaisePageState();
    }

    /// <summary>Rows with a pending delete are rendered grey and offer restore.</summary>
    protected override bool IsRowDeleted(DataNode element)
    {
        if (_deletedKeys.Count == 0) return false;
        string? key = KeyOf(element);
        return key is not null && _deletedKeys.Contains(key);
    }

    /// <summary>Only tracked rows can be deleted on a page table.</summary>
    public override bool CanDeleteRow => base.CanDeleteRow && CanTrack;

    protected override TableCell BuildRowOperationCell(DataNode element, int columnIndex, int visualRows)
    {
        List<TableCellAction> actions = [];

        if (RowEditorMode)
        {
            actions.Add(new TableCellAction(
                LocaleManager.Instance.Get("wpf.table.button.edit"),
                new RelayCommand(_ => EditRow(element), _ => !IsDisposed)));
        }

        if (CanTrack)
        {
            if (IsRowDeleted(element))
            {
                actions.Add(new TableCellAction(
                    LocaleManager.Instance.Get("wpf.button.resume"),
                    new RelayCommand(_ => ResumeRow(element), _ => !IsDisposed)));
            }
            else if (base.CanDeleteRow)
            {
                actions.Add(new TableCellAction(
                    LocaleManager.Instance.Get("wpf.button.delete"),
                    new RelayCommand(_ => DeleteRow(element), _ => CanDeleteRow && !IsDisposed)));
            }
        }

        return new TableCell(columnIndex, 0, visualRows, TableCellKind.RowOperation, null, actions);
    }

    /// <summary>Locales refresh for both the base table and the page chrome.</summary>
    protected override void OnLanguageChanged()
    {
        base.OnLanguageChanged();
        Post(() =>
        {
            RaisePropertyChanged(nameof(PageInfo));
            RaisePropertyChanged(nameof(LoadingText));
            RaisePropertyChanged(nameof(FirstText));
            RaisePropertyChanged(nameof(PreviousText));
            RaisePropertyChanged(nameof(NextText));
            RaisePropertyChanged(nameof(LastText));
            RaisePropertyChanged(nameof(QueryText));
            RaisePropertyChanged(nameof(ResetText));
            RaisePropertyChanged(nameof(TrackingHint));
            RaisePropertyChanged(nameof(HasTrackingHint));
        });
    }

    /// <summary>Accepts the tracked updates/deletions after the host persisted them.</summary>
    public void AcceptChanges()
    {
        _tracker.Clear();
        _deletedKeys.Clear();
        if (!IsDisposed) RebuildAll();
        RaisePageState();
    }

    #endregion

    #region Helpers

    private string? KeyOf(DataNode element)
    {
        if (!CanTrack || ArrayNodeImpl.Type is not ArrayType arrayType) return null;
        return arrayType.GetPrimaryKey(element);
    }

    private int ComputePageCount(int total)
    {
        if (total <= 0) return 0;
        int size = PageSize;
        return (total + size - 1) / size;
    }

    /// <summary>Loads a page by index, clamped to the current page count.</summary>
    public Task GoToPageAsync(int page)
    {
        int maxPage = Math.Max(0, PageCount - 1);
        return LoadPageAsync(Math.Clamp(page, 0, maxPage), _activeFilters);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CancelPendingLoads();
            ReleaseCurrentPage();
            _tracker.Clear();
            _deletedKeys.Clear();

            foreach (PageFilter filter in _filters)
                foreach (DataNodeWrapper input in filter.Inputs)
                    input.Dispose();
        }

        base.Dispose(disposing);
    }

    private sealed class RowTrack
    {
        public JsonNode? Update { get; set; }
        public bool Delete { get; set; }
    }

    #endregion
}
