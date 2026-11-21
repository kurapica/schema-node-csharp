using System.Text.Json.Nodes;
using SchemaNode.Context;
using SchemaNode.Data;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.WPF.AppView;

namespace SchemaNode.WPF.Man.Services;

/// <summary>
/// Host page source for an <c>AppFieldType</c>, backed by the configured <c>IAppDataProvider</c>
/// (Man local mode registers <c>InMemoryAppDataProvider</c>).
///
/// The adapter is the only place that knows how the host stores pages: the page table itself only
/// asks for a skip/take slice and a total, so Core keeps no page state and no PageNode is added.
/// </summary>
public sealed class ManAppFieldDataAdapter : IPageTableAdapter
{
    private readonly SchemaContext _context;
    private readonly AppFieldType _field;
    private readonly int _pageSize;

    public ManAppFieldDataAdapter(SchemaContext context, AppFieldType field, int pageSize)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _field = field ?? throw new ArgumentNullException(nameof(field));
        _pageSize = Math.Max(1, pageSize);
    }

    /// <inheritdoc />
    public int PageSize => _pageSize;

    /// <inheritdoc />
    public bool SupportsTracking => _field.ValueType is ArrayType { Primary.Count: > 0 };

    /// <inheritdoc />
    /// <remarks>
    /// Man maps only the four value filter modes. A function filter needs a host-side function
    /// evaluator, which this adapter intentionally does not provide: the page table reports the
    /// limitation instead of silently sending an unconstrained query.
    /// </remarks>
    public bool SupportsFunctionFilters => false;

    /// <inheritdoc />
    public async Task<PageTablePage> LoadPageAsync(PageTableQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        AppSchemaDataFilter? filter = BuildFilter(query.Filters);
        (IValueAccess? value, int total) = await _context
            .GetAppFieldDataAsync(_field, AppSchemaDataResult.List, filter, query.Skip, query.Take)
            .ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        if (value is not ArrayNode array)
            return new PageTablePage { Rows = [], Total = total };

        // The array is handed to the page table as its page token. Until that transfer happens the
        // adapter owns it, so a cancellation or a read failure inside this block must release it
        // instead of leaking an off-screen node.
        bool transferred = false;
        try
        {
            List<JsonNode?> rows = new(array.Count);
            foreach (IValueAccess row in array)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (row is DataNode dataNode)
                    rows.Add(dataNode.GetValue<JsonNode>());
            }

            // The source array stays alive as the page token; it is disposed when the table moves to
            // another page or leaves the tree, so off-screen pages are never retained.
            transferred = true;
            return new PageTablePage { Rows = rows, Total = total, Token = array };
        }
        finally
        {
            if (!transferred) (array as IDisposable)?.Dispose();
        }
    }

    /// <inheritdoc />
    public void ReleasePage(PageTablePage page)
    {
        (page.Token as IDisposable)?.Dispose();
    }

    /// <summary>Maps the light page filters to the Core app-data filter tree.</summary>
    private static AppSchemaDataFilter? BuildFilter(IReadOnlyList<PageTableFilterValue> filters)
    {
        AppSchemaDataFilter? result = null;

        foreach (PageTableFilterValue filter in filters)
        {
            LogicType logic = filter.Mode switch
            {
                FieldFilterMode.Exactly => LogicType.Equal,
                FieldFilterMode.Prefix => LogicType.StartsWith,
                FieldFilterMode.Suffix => LogicType.EndsWith,
                FieldFilterMode.Contains => LogicType.Contains,
                _ => LogicType.Equal, // function filters are rejected before this point
            };

            if (filter.Values.Count == 0 || filter.Values[0] is null) continue;

            AppSchemaDataFilterBinary item = new(
                logic,
                new AppSchemaDataFilterField(filter.Filter),
                new AppSchemaDataFilterValue(filter.Values[0]!));

            result = result is null ? item : result.AndAlso(item);
        }

        return result;
    }
}
