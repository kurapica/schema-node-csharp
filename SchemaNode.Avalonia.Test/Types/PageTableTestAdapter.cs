using System.Text.Json.Nodes;
using SchemaNode.Avalonia.AppView;

namespace SchemaNode.Avalonia.Test.Types;

/// <summary>
/// Host page source for the pageable harness case. Rows live in memory and are sliced by skip/take,
/// which is exactly the contract a real host adapter has to implement.
/// </summary>
internal sealed class PageTableTestAdapter : IPageTableAdapter
{
    private readonly IReadOnlyList<JsonNode> _rows;

    public PageTableTestAdapter(IReadOnlyList<JsonNode> rows, int pageSize)
    {
        _rows = rows;
        PageSize = Math.Max(1, pageSize);
    }

    /// <summary>Two rows per page turns five rows into three pages.</summary>
    public int PageSize { get; }

    /// <summary>The test element declares a primary key, so delete/restore can be tracked.</summary>
    public bool SupportsTracking => true;

    /// <summary>Value filters only; the harness does not configure function filters.</summary>
    public bool SupportsFunctionFilters => false;

    public Task<PageTablePage> LoadPageAsync(PageTableQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        List<JsonNode?> rows = _rows
            .Skip(query.Skip)
            .Take(query.Take)
            .Select(row => (JsonNode?)row.DeepClone())
            .ToList();

        return Task.FromResult(new PageTablePage { Rows = rows, Total = _rows.Count });
    }

    public void ReleasePage(PageTablePage page)
    {
        // Rows are plain JSON clones; nothing to release.
    }

    /// <summary>Five order lines, all with distinct primary keys.</summary>
    public static IReadOnlyList<JsonNode> CreateOrderLines()
    {
        List<JsonNode> rows = new(5);
        for (int i = 1; i <= 5; i++)
        {
            rows.Add(new JsonObject
            {
                ["code"] = $"P-{i}",
                ["quantity"] = i,
                ["unitPrice"] = i * 2.5m,
                ["discount"] = new JsonObject
                {
                    ["rate"] = 0.10m,
                    ["reason"] = "seasonal",
                },
            });
        }
        return rows;
    }
}
