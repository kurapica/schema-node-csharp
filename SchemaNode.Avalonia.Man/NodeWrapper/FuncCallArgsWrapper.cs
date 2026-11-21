using System.ComponentModel;
using System.Linq;
using SchemaNode.Avalonia.View;
using SchemaNode.Avalonia.View.Localization;
using SchemaNode.Avalonia.View.NodeWrapper;
using SchemaNode.Node;
using SchemaNode.Property.Common;
using SchemaNode.Property.String;
using SchemaNode.Property.Struct;
using SchemaNode.Runtime;
using SchemaNode.Struct;

namespace SchemaNode.Avalonia.Man.NodeWrapper;

/// <summary>One projected column of the function call argument list.</summary>
public sealed class FuncCallArgColumn
{
    internal FuncCallArgColumn(string fieldName, string header, double width)
    {
        FieldName = fieldName;
        Header = header;
        Width = width;
    }

    /// <summary>Struct field name used to resolve the cell node.</summary>
    public string FieldName { get; }

    /// <summary>Localized column header.</summary>
    public string Header { get; }

    /// <summary>Layout width shared by the header and every body cell.</summary>
    public double Width { get; }
}

/// <summary>One value cell of the argument row.</summary>
public sealed class FuncCallArgCell : INotifyPropertyChanged
{
    private bool _isEnabled;

    internal FuncCallArgCell(DataNodeWrapper? wrapper, double width, bool isEnabled)
    {
        Wrapper = wrapper;
        Width = width;
        _isEnabled = isEnabled;
    }

    /// <summary>
    /// Field wrapper rendered in this cell; null when the argument omits the field. The wrapper
    /// owns visibility, so a hidden field stays hidden.
    /// </summary>
    public DataNodeWrapper? Wrapper { get; }

    /// <summary>Layout width matching the column header.</summary>
    public double Width { get; }

    /// <summary>Whether the editor is interactive; follows the argument list's editability.</summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        private set
        {
            if (_isEnabled == value) return;
            _isEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
        }
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    internal void SetEnabled(bool value) => IsEnabled = value;
}

/// <summary>One function call argument rendered as a table row.</summary>
public sealed class FuncCallArgsRow
{
    internal FuncCallArgsRow(StructNode element, string label, double nameWidth, IReadOnlyList<FuncCallArgCell> cells)
    {
        Element = element;
        Label = label;
        NameWidth = nameWidth;
        Cells = cells;
    }

    /// <summary>Backing argument node, used to keep the row while the signature is unchanged.</summary>
    public StructNode Element { get; }

    /// <summary>Argument name, set by Core when the signature is rebuilt.</summary>
    public string Label { get; }

    /// <summary>Width of the argument-name column, shared with the header.</summary>
    public double NameWidth { get; }

    /// <summary>Field cells in column order.</summary>
    public IReadOnlyList<FuncCallArgCell> Cells { get; }
}

/// <summary>
/// Man-only view for the function call argument list (<c>system.list&lt;...callarg&gt;</c>).
///
/// The collection is owned by Core: <see cref="FuncCallArgsNode"/> rebuilds the argument nodes from
/// the selected function signature and <see cref="FuncCallArgsNode.SubscribeArgs"/> reports every
/// count change. This wrapper only projects those nodes, so the UI never offers add/delete and the
/// rows follow a function switch without keeping stale arguments. Every argument field remains an
/// ordinary editable leaf view; only the collection itself is read-only.
/// </summary>
public sealed class FuncCallArgsWrapper : DataNodeWrapper
{
    private const double NameColumnWidth = 110;
    private readonly List<StructFieldType> _fields = [];

    public FuncCallArgsWrapper(DataNode node, SchemaViewRegistry registry)
        : base(node, registry, null, SchemaViewRole.Field)
    {
        ArgsNodeImpl = node as FuncCallArgsNode
            ?? throw new ArgumentException($"Node '{node.Type.Name}' is not a function call argument list.", nameof(node));

        BuildColumns();
        ReconcileRows();
        Track(ArgsNodeImpl.SubscribeArgs(_ => Post(ReconcileRows)));
    }

    /// <summary>Typed Core node.</summary>
    public FuncCallArgsNode ArgsNodeImpl { get; }

    /// <summary>A container only groups argument cells; the failing leaf shows its own error.</summary>
    protected override bool RendersValue => false;

    /// <summary>Width of the argument-name column.</summary>
    public double NameWidth => NameColumnWidth;

    /// <summary>Localized argument-name column header.</summary>
    public string NameHeader => LocaleManager.Instance.Get("avalonia.funccallargs.header.name");

    /// <summary>Projected field columns, in schema order, excluding hidden and computed columns.</summary>
    public BulkObservableCollection<FuncCallArgColumn> Columns { get; } = new();

    /// <summary>Argument rows, rebuilt in place when the signature changes.</summary>
    public BulkObservableCollection<FuncCallArgsRow> Rows { get; } = new();

    /// <summary>Whether the argument list is empty.</summary>
    public bool IsEmpty => Rows.Count == 0;

    /// <summary>Localized empty hint.</summary>
    public string EmptyText => LocaleManager.Instance.Get("avalonia.funccallargs.empty");

    private void BuildColumns()
    {
        _fields.Clear();

        List<FuncCallArgColumn> columns = [];
        if ((ArgsNodeImpl.Type as ArrayType)?.Element is StructType elementType)
        {
            foreach (StructFieldType field in elementType.GetFields())
            {
                // The vue callArgs view projects every field that is not hidden at schema level.
                // sourceType is a computed mirror of type, so it is skipped in both frontends.
                if (field.GetProperty<InVisible>()?.Value == true) continue;
                if (field.GetProperty<AccessValueTypeResolver>() is not null) continue;

                _fields.Add(field);
                columns.Add(new FuncCallArgColumn(field.Name, HeaderFor(field), WidthFor(field)));
            }
        }

        Columns.ReplaceAll(columns);
    }

    private void RefreshHeaders()
    {
        if (IsDisposed) return;
        List<FuncCallArgColumn> columns = _fields
            .Select(field => new FuncCallArgColumn(field.Name, HeaderFor(field), WidthFor(field)))
            .ToList();
        Columns.ReplaceAll(columns);
        RaisePropertyChanged(nameof(EmptyText));
        RaisePropertyChanged(nameof(NameHeader));
    }

    private void ReconcileRows()
    {
        if (IsDisposed) return;

        List<StructNode> elements = ArgsNodeImpl.Cast<StructNode>().ToList();
        if (elements.Count == Rows.Count
            && elements.Select((element, i) => ReferenceEquals(element, Rows[i].Element)).All(same => same))
            return;

        List<FuncCallArgsRow> next = [];
        int variadicStart = ArgsNodeImpl.Length - (ArgsNodeImpl.VariadicArg?.Count ?? 0);
        int index = 0;
        foreach (StructNode element in elements)
        {
            List<FuncCallArgCell> cells = [];
            foreach (StructFieldType field in _fields)
            {
                DataNodeWrapper? wrapper = element.GetAccessValue(field.Name) is DataNode node
                    ? NodeWrapperFactory.Create(node, Registry, field.Name, SchemaViewRole.Cell)
                    : null;
                cells.Add(new FuncCallArgCell(wrapper, WidthFor(field), IsEditable));
            }

            string label = element.Name ?? string.Empty;
            if (string.IsNullOrEmpty(label) && index >= variadicStart)
                label = ArgsNodeImpl.VariadicArg?.Name ?? string.Empty;

            next.Add(new FuncCallArgsRow(element, label, NameColumnWidth, cells));
            index++;
        }

        DisposeCells(Rows);
        Rows.ReplaceAll(next);
        RaisePropertyChanged(nameof(IsEmpty));
    }

    /// <inheritdoc />
    protected override void OnLanguageChanged() => Post(RefreshHeaders);

    /// <inheritdoc />
    protected override void OnEditableChanged()
    {
        foreach (FuncCallArgsRow row in Rows)
            foreach (FuncCallArgCell cell in row.Cells)
                cell.SetEnabled(IsEditable);
    }

    private static void DisposeCells(IEnumerable<FuncCallArgsRow> rows)
    {
        foreach (FuncCallArgsRow row in rows)
            foreach (FuncCallArgCell cell in row.Cells)
                cell.Wrapper?.Dispose();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !IsDisposed)
        {
            DisposeCells(Rows);
            Rows.ReplaceAll([]);
            Columns.ReplaceAll([]);
            _fields.Clear();
        }
        base.Dispose(disposing);
    }

    private string HeaderFor(StructFieldType field)
    {
        LocaleString? display = field.GetProperty<Display>()?.Value;
        if (display is not null)
        {
            try
            {
                string text = display.ToString(ArgsNodeImpl.Context);
                if (!string.IsNullOrWhiteSpace(text) && !text.Contains('{') && !text.Contains('}'))
                    return text;
            }
            catch
            {
                // A display key that no longer resolves must not break the whole table.
            }
        }

        if (LocaleManager.Instance.TryGet($"avalonia.funccallargs.header.{field.Name}", out string translated))
            return translated;
        if (LocaleManager.Instance.TryGet(field.Name, out translated))
            return translated;
        return field.Name;
    }

    private static double WidthFor(StructFieldType field)
    {
        if (string.Equals(field.Name, "type", StringComparison.OrdinalIgnoreCase)) return 160;
        if (field.Type is StructType or ArrayType) return 240;
        return 200;
    }
}
