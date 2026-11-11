using System.Text.Json.Nodes;
using SchemaNode.Enum;

namespace SchemaNode.Avalonia.AppView;

/// <summary>
/// Host-supplied page source for <see cref="NodeWrapper.PageTableWrapper"/>.
///
/// C# Core has no PageNode: the paging state (<c>skip</c>/<c>take</c> and <c>total</c>) belongs to
/// the host query result, not to the element type. The host therefore owns the data source and
/// only exposes page loading, total counting and page release through this interface.
/// </summary>
public interface IPageTableAdapter
{
    /// <summary>Number of rows requested per page (<c>take</c>). Must be greater than zero.</summary>
    int PageSize { get; }

    /// <summary>
    /// Whether the host can track rows across pages by primary key. When false, the page table
    /// disables delete/restore and shows an explicit degradation notice instead of pretending
    /// that deletions are pending.
    /// </summary>
    bool SupportsTracking { get; }

    /// <summary>
    /// Whether the host can translate function filters. When false, an active function filter is
    /// rejected explicitly instead of being silently ignored.
    /// </summary>
    bool SupportsFunctionFilters { get; }

    /// <summary>Loads one page. Implementations should honour <paramref name="cancellationToken"/>.</summary>
    Task<PageTablePage> LoadPageAsync(PageTableQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Releases the backing data of a page that is no longer displayed. The table calls this for
    /// the previous page after a successful navigation and for results that arrive after the user
    /// already moved on. Implementations without a backing node can no-op.
    /// </summary>
    void ReleasePage(PageTablePage page);
}

/// <summary>One page request: the window and the active filters.</summary>
public sealed record PageTableQuery(int Skip, int Take, IReadOnlyList<PageTableFilterValue> Filters);

/// <summary>One active filter value, taken from the field's <c>Filters</c> property inputs.</summary>
public sealed record PageTableFilterValue(string Filter, FieldFilterMode Mode, IReadOnlyList<object?> Values);

/// <summary>One loaded page: the rows, the total result count and an optional host release token.</summary>
public sealed class PageTablePage
{
    /// <summary>Rows of the current page, in display order.</summary>
    public IReadOnlyList<JsonNode?> Rows { get; init; } = [];

    /// <summary>Total rows matching the active filters (not just the current page).</summary>
    public int Total { get; init; }

    /// <summary>Opaque host token released through <see cref="IPageTableAdapter.ReleasePage"/>.</summary>
    public object? Token { get; init; }
}
