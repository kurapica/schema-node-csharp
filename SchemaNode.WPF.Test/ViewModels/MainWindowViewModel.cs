using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using Prism.Mvvm;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property.Common;
using SchemaNode.Property.Int;
using SchemaNode.Runtime;
using SchemaNode.WPF.Test.Type;
using SchemaNode.WPF.AppView.NodeWrapper;
using SchemaNode.WPF.View;
using SchemaNode.WPF.View.Localization;
using SchemaNode.WPF.View.NodeWrapper;

namespace SchemaNode.WPF.Test.ViewModels;

/// <summary>Registry a case resolves its views through.</summary>
internal enum SampleRegistry
{
    /// <summary>Default table registry; struct arrays render as tables.</summary>
    Table,

    /// <summary>Table registry with view-columns so rows only edit through the row editor.</summary>
    RowEditor,

    /// <summary>Table registry with a restricted view-columns projection.</summary>
    Restricted,
}

/// <summary>One entry of the case picker.</summary>
internal sealed class SampleCase
{
    public SampleCase(string key, string title, bool tableCapable = true, SampleRegistry registry = SampleRegistry.Table)
    {
        Key = key;
        Title = title;
        TableCapable = tableCapable;
        Registry = registry;
    }

    public string Key { get; }

    public string Title { get; }

    public bool TableCapable { get; }

    public SampleRegistry Registry { get; }

    /// <inheritdoc />
    public override string ToString() => Title;
}

/// <summary>One entry of the locale picker. Switching it also switches Core's validation language.</summary>
internal sealed class SampleLocale
{
    public SampleLocale(string code)
    {
        Code = code;
    }

    public string Code { get; }

    /// <inheritdoc />
    public override string ToString() => Code;
}

/// <summary>
/// Manual harness. It renders exactly one schema case at a time so every node shape can be
/// inspected in isolation, and it prints the node's own value under the form so the write-back is
/// visible while typing.
/// </summary>
internal class MainWindowViewModel : BindableBase, IDisposable
{
    private readonly SchemaContext _context;
    private readonly SchemaViewRegistry _formRegistry;
    private readonly SchemaViewRegistry _tableRegistry;
    private readonly SchemaViewRegistry _rowEditorRegistry;
    private readonly SchemaViewRegistry _restrictedRegistry;
    private SampleCase? _selectedCase;
    private IValueAccess? _node;
    private IDisposable? _nodeSubscription;
    private PageTableWrapper? _pageTableWrapper;
    private DataNode? _pageTableNode;
    private IDisposable? _pageTableSubscription;
    private string? _nodeValue;
    private string? _status;
    private bool _formArrays;
    private int _loadVersion;

    public MainWindowViewModel(SchemaContext context)
    {
        _context = context;

        // Registering the element type as a "single view" is what makes an array of structs render
        // through the array/form view; the table registry instead marks the array type as table.
        _formRegistry = SchemaViewRegistry.CreateDefault();
        _formRegistry.RegisterType("test.minmax", SchemaTemplateKeys.Struct, asSingle: true);
        _formRegistry.RegisterType("test.orderline", SchemaTemplateKeys.Struct, asSingle: true);
        _formRegistry.RegisterType("test.invoice", SchemaTemplateKeys.Struct, asSingle: true);

        // OrderLine and Invoice declare a primary index, so Core names their companion array types
        // test.orderlines / test.invoices. Those generated names are what the host registers.
        _tableRegistry = SchemaViewRegistry.CreateDefault();
        _tableRegistry.RegisterTable("system.list<test.minmax>");
        _tableRegistry.RegisterTable("test.orderlines");
        _tableRegistry.RegisterTable("test.invoices");

        _rowEditorRegistry = SchemaViewRegistry.CreateDefault();
        _rowEditorRegistry.RegisterTable("test.orderlines");
        _rowEditorRegistry.RegisterTable("test.invoices", new SchemaTableOptions
        {
            ViewColumns = ["number", "customer", "lines.code", "lines.quantity"],
        });

        _restrictedRegistry = SchemaViewRegistry.CreateDefault();
        _restrictedRegistry.RegisterTable("test.orderlines", new SchemaTableOptions
        {
            ViewColumns = ["code", "discount.rate"],
        });

        Cases =
        [
            new SampleCase("string", "string"),
            new SampleCase("int", "int"),
            new SampleCase("decimal", "decimal"),
            new SampleCase("date", "date"),
            new SampleCase("bool", "bool"),
            new SampleCase("boolpair", "bool (required + optional)"),
            new SampleCase("enum", "enum (single level)", false),
            new SampleCase("enumarray", "enum array (multi-select)", false),
            new SampleCase("array", "simple array"),
            new SampleCase("struct", "struct (MinMax)"),
            new SampleCase("structarray", "struct array (form / table)"),
            new SampleCase("nested", "nested struct + array"),
            new SampleCase("orderarray", "table: struct array + nested struct"),
            new SampleCase("invoicearray", "table: struct array + struct-array field"),
            new SampleCase("invoiceedit", "table: row editor + add row", true, SampleRegistry.RowEditor),
            new SampleCase("orderrestricted", "table: restricted view-columns", true, SampleRegistry.Restricted),
            new SampleCase("pageable", "pageable: paged table + tracker"),
            new SampleCase("readonly", "read-only struct"),
            new SampleCase("required", "required string"),
            new SampleCase("limited", "limited int"),
        ];

        Locales =
        [
            new SampleLocale(LocaleManager.ZH_CN),
            new SampleLocale(LocaleManager.EN_US),
        ];
        _selectedLocale = Locales[0];
    }

    /// <summary>Cases shown by the picker.</summary>
    public ObservableCollection<SampleCase> Cases { get; }

    /// <summary>Locales shown by the picker. Switching one switches Core validation text too.</summary>
    public ObservableCollection<SampleLocale> Locales { get; }

    private SampleLocale _selectedLocale;

    /// <summary>
    /// Selected locale. Core reads <c>SchemaContext.Locale</c> when it composes validation text,
    /// while the view layer keeps its own copy for UI labels, so both have to switch together.
    /// Reloading the current case rebuilds the wrappers and re-evaluates the error text.
    /// </summary>
    public SampleLocale SelectedLocale
    {
        get => _selectedLocale;
        set
        {
            if (!SetProperty(ref _selectedLocale, value) || value is null) return;
            _context.Locale = value.Code;
            LocaleManager.Instance.SetLocale(value.Code);
            if (_started && _selectedCase is not null) _ = LoadAsync(_selectedCase);
        }
    }

    /// <summary>
    /// Registry set once on the root view and inherited by nested views. Cases that exercise the
    /// row editor or the restricted whitelist pick their own registry; the form toggle only applies
    /// to table-capable cases.
    /// </summary>
    public SchemaViewRegistry Registry => _formArrays && _selectedCase?.TableCapable == true
        ? _formRegistry
        : _selectedCase?.Registry switch
        {
            SampleRegistry.RowEditor => _rowEditorRegistry,
            SampleRegistry.Restricted => _restrictedRegistry,
            _ => _tableRegistry,
        };

    /// <summary>Whether arrays of structs render through the array/form view instead of the table view.</summary>
    public bool FormArrays
    {
        get => _formArrays;
        set
        {
            if (SetProperty(ref _formArrays, value))
                RaisePropertyChanged(nameof(Registry));
        }
    }

    /// <summary>Currently selected case; selecting one rebuilds the node.</summary>
    public SampleCase? SelectedCase
    {
        get => _selectedCase;
        set
        {
            if (!SetProperty(ref _selectedCase, value) || value is null || !_started) return;
            RaisePropertyChanged(nameof(Registry));
            _ = LoadAsync(value);
        }
    }

    /// <summary>The root node rendered by <c>SchemaView</c>.</summary>
    public IValueAccess? Node
    {
        get => _node;
        private set => SetProperty(ref _node, value);
    }

    /// <summary>Latest node value, printed under the form so the write-back is visible.</summary>
    public string? NodeValue
    {
        get => _nodeValue;
        private set => SetProperty(ref _nodeValue, value);
    }

    /// <summary>
    /// Page table shown by the pageable case. The harness owns the wrapper so page navigation,
    /// edits and delete/restore can be asserted instead of only rendered.
    /// </summary>
    public PageTableWrapper? PageTable
    {
        get => _pageTableWrapper;
        private set
        {
            if (!SetProperty(ref _pageTableWrapper, value)) return;
            RaisePropertyChanged(nameof(ShowPageTable));
            RaisePropertyChanged(nameof(ShowSchemaView));
        }
    }

    /// <summary>Whether the page table panel replaces the regular schema view.</summary>
    public bool ShowPageTable => _pageTableWrapper is not null;

    /// <summary>Whether the regular single-node schema view is shown.</summary>
    public bool ShowSchemaView => !ShowPageTable;

    /// <summary>What the harness is doing, including failures the harness itself caused.</summary>
    public string? Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    private bool _started;

    /// <summary>
    /// Called by the application only after <c>InitSchemaRuntimeAsync</c> completed, so the runtime
    /// already knows the CLR types of this assembly and the locale is already applied.
    /// </summary>
    public async Task InitializeAsync()
    {
        SampleCase first = Cases[0];
        _selectedCase = first;
        RaisePropertyChanged(nameof(SelectedCase));
        await LoadAsync(first);
        _started = true;
    }

    private async Task LoadAsync(SampleCase sampleCase)
    {
        int version = ++_loadVersion;
        Status = $"loading {sampleCase.Title}…";
        IValueAccess? next = null;
        string? failure = null;
        try
        {
            next = await CreateAsync(sampleCase.Key);
        }
        catch (Exception ex)
        {
            failure = ex.Message;
        }

        if (version != _loadVersion)
        {
            if (next is IDisposable stale) stale.Dispose();
            return;
        }

        ReleaseNode();

        if (sampleCase.Key == "pageable" && next is DataNode pageNode)
        {
            try
            {
                await SetupPageTableAsync(pageNode);
            }
            catch (Exception ex)
            {
                Status = $"{sampleCase.Title} failed: {ex.Message}";
            }
            return;
        }

        Node = next;
        if (next is null)
        {
            Status = failure is null
                ? $"no node for {sampleCase.Title}"
                : $"{sampleCase.Title} failed: {failure}";
            return;
        }

        try
        {
            _nodeSubscription = next.Subscribe(items => NodeValue = Format(items.Item2), immediate: true);
            string? inner = NestedArrayTypeName(next);
            Status = $"{sampleCase.Title} · {next.Type?.Name}" + (inner is null ? string.Empty : $" · inner {inner}");
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    /// <summary>Builds each case from real Core nodes: CLR value types, enums, arrays and structs.</summary>
    private async Task<IValueAccess?> CreateAsync(string key)
    {
        switch (key)
        {
            case "string":
                return await _context.GetSchemaNodeAsync(new StringCase());
            case "int":
                return await _context.GetSchemaNodeAsync(new IntCase());
            case "decimal":
                return await _context.GetSchemaNodeAsync(new DecimalCase());
            case "date":
                return await _context.GetSchemaNodeAsync(new DateCase());
            case "bool":
                return await _context.GetSchemaNodeAsync(new BoolCase());
            case "boolpair":
                return await _context.GetSchemaNodeAsync(new BoolPairCase());
            case "enum":
                return await _context.GetSchemaNodeAsync(new EnumCase());
            case "enumarray":
            {
                IValueTypeAccess? element = await _context.GetNodeTypeAsync<EnumType>("test.mode");
                return await CreateArrayAsync(element, new object?[] { "alpha", "gamma" }, "test.mode[]");
            }
            case "array":
                return await _context.GetSchemaNodeAsync(new ArrayCase());
            case "struct":
                return await _context.GetSchemaNodeAsync(new MinMax { Min = 2, Max = 3 });
            case "structarray":
            {
                IValueTypeAccess? element = await _context.GetNodeTypeAsync<SchemaNode.Runtime.ValueType>("test.minmax");
                return await CreateArrayAsync(
                    element,
                    new object?[] { new MinMax { Min = 1, Max = 2 }, new MinMax { Min = 3, Max = 4 } },
                    "test.minmax[]");
            }
            case "nested":
            {
                // A struct whose fields are themselves a struct and an array of structs cannot be
                // built from one raw CLR object (Core's object path only converts scalar lists), so
                // the empty node is created from its type and the fields are filled by access.
                IValueTypeAccess? type = await _context.GetNodeTypeAsync<SchemaNode.Runtime.ValueType>("test.nested");
                if (type is null)
                    throw new InvalidOperationException("schema type 'test.nested' is not registered");

                IValueAccess? node = type.Create(_context);
                if (node is null)
                    throw new InvalidOperationException("'test.nested' created no node");

                // Fill through the field accessors, the same path the editor takes. Assigning a
                // whole CLR object to a struct node (TrySetValue(MinMax)) used to lose the
                // reflection target halfway through the field loop in FullMode; that is fixed in
                // StructNode now, but field-by-field filling keeps this sample independent of it.
                IValueAccess? inner = node.GetAccessValue("inner");
                inner?.GetAccessValue("min")?.TrySetValue(5);
                inner?.GetAccessValue("max")?.TrySetValue(6);

                if (node.GetAccessValue("lines") is ArrayNode rows)
                {
                    IValueTypeAccess? element = await _context.GetNodeTypeAsync<SchemaNode.Runtime.ValueType>("test.minmax");
                    IValueAccess? row = element?.Create(_context, new MinMax { Min = 7, Max = 8 });
                    if (row is not null) rows.Add(row);
                }
                return node;
            }
            case "orderarray":
                return await CreateOrderArrayAsync();
            case "invoicearray":
            case "invoiceedit":
                return await CreateInvoiceArrayAsync();
            case "orderrestricted":
                return await CreateOrderArrayAsync();
            case "pageable":
            {
                IValueTypeAccess? element = await _context.GetNodeTypeAsync<SchemaNode.Runtime.ValueType>("test.orderline");
                ArrayType? arrayType = element is null ? null : await _context.GetArrayNodeTypeAsync(element);
                if (arrayType?.Create(_context) is not ArrayNode empty)
                    throw new InvalidOperationException("'test.orderline' has no array type");
                return empty;
            }
            case "readonly":
            {
                IValueAccess? node = await _context.GetSchemaNodeAsync(new MinMax { Min = 2, Max = 3 });
                node?.SetPropertyValue<ReadOnly, bool>(true, null, null);
                return node;
            }
            case "required":
            {
                IValueAccess? node = await _context.GetSchemaNodeAsync(new RequiredCase());
                node?.GetAccessValue("text")?.SetPropertyValue<Require, bool>(true, null, null);
                return node;
            }
            case "limited":
            {
                IValueAccess? node = await _context.GetSchemaNodeAsync(new IntCase());
                node?.GetAccessValue("count")?.SetPropertyValue<LowLimitInt, long>(10L, null, null);
                return node;
            }
            default:
                return null;
        }
    }

    /// <summary>Reports the first nested array type so the generated "-s"/"s" name is visible in the status line.</summary>
    private static string? NestedArrayTypeName(IValueAccess node)
    {
        if (node is not ArrayNode array || array.ElementType is not StructType element) return null;
        foreach (StructFieldType field in element.GetFields())
            if (field.Type is ArrayType inner) return inner.Name;
        return null;
    }

    private async Task<IValueAccess?> CreateOrderArrayAsync()
    {
        DataNode[] lines =
        [
            await CreateOrderLineAsync("L-1", 2, 9.5m, 0.10m, "seasonal"),
            await CreateOrderLineAsync("L-2", 4, 3.25m, 0m, "none"),
        ];
        return await CreateStructArrayAsync("test.orderline", lines);
    }

    private async Task<IValueAccess?> CreateInvoiceArrayAsync()
    {
        DataNode[] invoices =
        [
            await CreateInvoiceAsync(
                "INV-1",
                "ACME",
                ("L-1", 2, 9.5m, 0.10m, "seasonal"),
                ("L-2", 4, 3.25m, 0m, "none")),
            await CreateInvoiceAsync(
                "INV-2",
                "Globex",
                ("L-3", 1, 120m, 0.05m, "partner")),
        ];
        return await CreateStructArrayAsync("test.invoice", invoices);
    }

    private async Task<DataNode> CreateOrderLineAsync(
        string code,
        int quantity,
        decimal unitPrice,
        decimal rate,
        string reason)
    {
        IValueTypeAccess? type = await _context.GetNodeTypeAsync<SchemaNode.Runtime.ValueType>("test.orderline");
        IValueAccess? node = type?.Create(_context);
        if (node is null)
            throw new InvalidOperationException("'test.orderline' created no node");

        node.GetAccessValue("code")?.TrySetValue(code);
        node.GetAccessValue("quantity")?.TrySetValue(quantity);
        node.GetAccessValue("unitPrice")?.TrySetValue(unitPrice);

        IValueAccess? discount = node.GetAccessValue("discount");
        discount?.GetAccessValue("rate")?.TrySetValue(rate);
        discount?.GetAccessValue("reason")?.TrySetValue(reason);

        return (DataNode)node;
    }

    private async Task<DataNode> CreateInvoiceAsync(
        string number,
        string customer,
        params (string Code, int Quantity, decimal Price, decimal Rate, string Reason)[] lines)
    {
        IValueTypeAccess? type = await _context.GetNodeTypeAsync<SchemaNode.Runtime.ValueType>("test.invoice");
        IValueAccess? node = type?.Create(_context);
        if (node is null)
            throw new InvalidOperationException("'test.invoice' created no node");

        node.GetAccessValue("number")?.TrySetValue(number);
        node.GetAccessValue("customer")?.TrySetValue(customer);

        if (node.GetAccessValue("lines") is not ArrayNode rows)
            throw new InvalidOperationException("'test.invoice.lines' is not an array node");

        foreach ((string code, int quantity, decimal price, decimal rate, string reason) in lines)
            rows.AddRow(null, await CreateOrderLineAsync(code, quantity, price, rate, reason));

        return (DataNode)node;
    }

    /// <summary>
    /// Creates an array node from the generated array type of the element, so the type name is the
    /// Core-generated one (struct name plus the <c>s</c> suffix when the struct declares a primary index).
    /// </summary>
    private async Task<IValueAccess?> CreateStructArrayAsync(string elementTypeName, IReadOnlyList<DataNode> elements)
    {
        IValueTypeAccess? element = await _context.GetNodeTypeAsync<SchemaNode.Runtime.ValueType>(elementTypeName);
        if (element is null)
            throw new InvalidOperationException($"'{elementTypeName}' is not registered");

        ArrayType? arrayType = await _context.GetArrayNodeTypeAsync(element);
        IValueAccess? node = arrayType?.Create(_context);
        if (node is not ArrayNode array)
            throw new InvalidOperationException($"'{elementTypeName}' has no array type");

        foreach (DataNode elementNode in elements)
        {
            if (array.AddRow(null, elementNode) is null)
                throw new InvalidOperationException($"'{elementTypeName}' rejected an element");
        }

        return node;
    }

    /// <summary>
    /// Builds an array node from its element type. A plain CLR array value cannot be handed to
    /// <c>GetSchemaNodeAsync</c> when the element is a struct or an enum: that path only knows the
    /// built-in scalar list types, so the array type is resolved from the element type instead and
    /// the value is converted element by element.
    /// </summary>
    private async Task<IValueAccess?> CreateArrayAsync(IValueTypeAccess? element, object?[] items, string typeName)
    {
        if (element is null)
            throw new InvalidOperationException($"element type of '{typeName}' is not registered");

        ArrayType? arrayType = await _context.GetArrayNodeTypeAsync(element);
        if (arrayType is null)
            throw new InvalidOperationException($"'{typeName}' has no array type");

        IValueAccess? node = arrayType.Create(_context);
        if (node is null)
            throw new InvalidOperationException($"'{typeName}' could not create an array node");

        // Enum arrays hold a JsonArray of keys; plain arrays accept one converted element per item.
        if (node is EnumArrayNode enumArray)
        {
            JsonArray keys = new(items.Select(item => JsonValue.Create(item)).ToArray());
            enumArray.TrySetValue(keys);
        }
        else if (node is ArrayNode array)
        {
            foreach (object? item in items)
            {
                IValueAccess? elementNode = element.Create(_context, item);
                if (elementNode is null)
                    throw new InvalidOperationException($"'{typeName}' could not convert an element");
                array.Add(elementNode);
            }
        }
        else
        {
            throw new InvalidOperationException($"'{typeName}' created no collection node");
        }

        return node;
    }

    /// <summary>
    /// Builds the pageable harness case: a five-row host source with two rows per page, then drives
    /// navigation, one cell edit and one delete/restore through the paginated surface and reports
    /// the results in the status line.
    /// </summary>
    private async Task SetupPageTableAsync(DataNode node)
    {
        PageTableWrapper wrapper = new(node, Registry, new PageTableTestAdapter(PageTableTestAdapter.CreateOrderLines(), 2));
        _pageTableNode = node;
        _pageTableSubscription = node.Subscribe(items => NodeValue = Format(items.Item2), immediate: true);
        PageTable = wrapper;

        await wrapper.EnsureLoadedAsync();
        await WaitIdleAsync(wrapper);

        List<string> checks = [];
        checks.Add(Check("total", wrapper.Total == 5));
        checks.Add(Check("pages", wrapper.PageCount == 3));
        checks.Add(Check("first", wrapper.Page == 0 && wrapper.RowGroups.Count == 2));

        await wrapper.GoToPageAsync(1);
        await WaitIdleAsync(wrapper);
        checks.Add(Check("next", wrapper.Page == 1 && wrapper.RowGroups.Count == 2));

        await wrapper.GoToPageAsync(2);
        await WaitIdleAsync(wrapper);
        checks.Add(Check("last", wrapper.Page == 2 && wrapper.RowGroups.Count == 1));

        await wrapper.GoToPageAsync(0);
        await WaitIdleAsync(wrapper);

        // Edit through the rendered cell wrapper, then reload the page: the tracker has to reapply it.
        TableRowGroup first = wrapper.RowGroups[0];
        TableCell? quantityCell = first.Cells.FirstOrDefault(
            cell => cell.Kind == TableCellKind.Value && cell.Wrapper?.FieldName == "quantity");
        bool edited = quantityCell?.Wrapper?.Node.TrySetValue(99) == true;

        await wrapper.GoToPageAsync(1);
        await WaitIdleAsync(wrapper);
        await wrapper.GoToPageAsync(0);
        await WaitIdleAsync(wrapper);

        ArrayNode page = (ArrayNode)node;
        JsonObject? reloaded = page.ElementAt(0) is DataNode row
            ? row.GetValue<JsonNode>() as JsonObject
            : null;
        int quantity = reloaded?["quantity"]?.GetValue<int>() ?? 0;
        checks.Add(Check("edit", edited && quantity == 99 && wrapper.PendingUpdates.Count == 1));

        // Delete and restore through the operations column of the page table.
        TableCell deleteCell = OperationCell(wrapper);
        TableCellAction? delete = deleteCell.Actions.FirstOrDefault(
            action => action.Label == LocaleManager.Instance.Get("wpf.button.delete"));
        delete?.Command?.Execute(null);
        checks.Add(Check("delete", wrapper.DeletedKeys.Count == 1 && wrapper.RowGroups[0].IsDeleted));

        TableCell resumeCell = OperationCell(wrapper);
        TableCellAction? resume = resumeCell.Actions.FirstOrDefault(
            action => action.Label == LocaleManager.Instance.Get("wpf.button.resume"));
        resume?.Command?.Execute(null);
        checks.Add(Check("resume", wrapper.DeletedKeys.Count == 0 && !wrapper.RowGroups[0].IsDeleted));

        Status = $"pageable · {string.Join(" · ", checks)}";
    }

    /// <summary>The operations cell of the first page row.</summary>
    private static TableCell OperationCell(PageTableWrapper wrapper)
        => wrapper.RowGroups[0].Cells.First(cell => cell.Kind == TableCellKind.RowOperation);

    /// <summary>Waits until the posted page apply has run, so assertions observe the settled state.</summary>
    private static async Task WaitIdleAsync(PageTableWrapper wrapper)
    {
        for (int i = 0; i < 100 && wrapper.IsLoading; i++)
            await Task.Delay(10);
    }

    private static string Check(string name, bool passed) => passed ? $"{name} ok" : $"{name} FAILED";

    private void ReleaseNode()
    {
        _nodeSubscription?.Dispose();
        _nodeSubscription = null;
        _pageTableSubscription?.Dispose();
        _pageTableSubscription = null;

        // The wrapper owns page state only; the node is released separately below.
        PageTable?.Dispose();
        PageTable = null;

        IValueAccess? pageNode = _pageTableNode;
        _pageTableNode = null;
        if (pageNode is IDisposable pageDisposable) pageDisposable.Dispose();

        IValueAccess? node = _node;
        Node = null;
        if (node is IDisposable disposable) disposable.Dispose();
        NodeValue = null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        ReleaseNode();
    }

    private static string Format(object? value)
        => value is null ? "(empty)" : value.ToString() ?? "(empty)";
}
