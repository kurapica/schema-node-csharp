using System.Windows.Input;
using SchemaNode.Node;
using SchemaNode.Property.Common;
using SchemaNode.Runtime;
using SchemaNode.Struct;
using SchemaNode.WPF.View.Localization;

namespace SchemaNode.WPF.View.NodeWrapper;

/// <summary>One table column, derived from a struct field of the element type.</summary>
public sealed class TableColumn
{
    public TableColumn(string fieldName, string header, bool require)
    {
        FieldName = fieldName;
        Header = header;
        Require = require;
    }

    /// <summary>Schema field name.</summary>
    public string FieldName { get; }

    /// <summary>Localized header text.</summary>
    public string Header { get; }

    /// <summary>Whether the field is required (rendered with a marker).</summary>
    public bool Require { get; }
}

/// <summary>One table row, backed by (and reusing) its element <see cref="StructWrapper"/>.</summary>
public sealed class TableRow
{
    internal TableRow(int index, StructWrapper element, TableColumn[] columns)
    {
        Index = index;
        Element = element;
        Cells = columns.Select(c => element.GetChild(c.FieldName)).Where(c => c is not null).Cast<DataNodeWrapper>().ToArray();
    }

    /// <summary>Zero-based row index.</summary>
    public int Index { get; internal set; }

    /// <summary>Element wrapper shared with the incremental child cache.</summary>
    public StructWrapper Element { get; }

    /// <summary>Cell wrappers in column order.</summary>
    public IReadOnlyList<DataNodeWrapper> Cells { get; }

    /// <summary>Removes this row.</summary>
    public ICommand? RemoveCommand => Element.RemoveCommand;
}

/// <summary>
/// Wraps an array of structs rendered as a table.
///
/// Columns are derived once from the element schema (fields carrying InVisible are skipped) and
/// rebuilt only on a language switch. Rows are reconciled by element node identity, so virtualization
/// can recycle containers without rebuilding the wrapper tree.
/// </summary>
public sealed class TableWrapper : DataNodeWrapper
{
    private readonly Dictionary<DataNode, StructWrapper> _rowByNode = [];
    private bool _reconciling;

    /// <inheritdoc />
    public override string? DefaultTemplateKey => SchemaTemplateKeys.Table;

    public TableWrapper(DataNode node, SchemaViewRegistry? registry, string? fieldName, SchemaViewRole role)
        : base(node, registry, fieldName, role)
    {
        ArrayNodeImpl = node as ArrayNode
            ?? throw new ArgumentException($"Node '{node.Type.Name}' is not an array node.", nameof(node));
        StructType? elementType = ArrayNodeImpl.ElementType as StructType
            ?? throw new ArgumentException($"Array '{node.Type.Name}' has no struct element type.", nameof(node));
        ElementType = elementType;

        AddCommand = new RelayCommand(_ => AddRow(), _ => IsEditable);
        RemoveLastCommand = new RelayCommand(_ => RemoveLast(), _ => IsEditable && Rows.Count > 0);

        BuildColumns();
        ReconcileRows();
        Track(node.Subscribe(OnArrayDataChanged, immediate: false));
    }

    /// <summary>Typed array node.</summary>
    public ArrayNode ArrayNodeImpl { get; }

    /// <summary>Struct element type used to derive columns.</summary>
    public StructType ElementType { get; }

    /// <summary>Columns, in schema order.</summary>
    public BulkObservableCollection<TableColumn> Columns { get; } = new();

    /// <summary>Rows, in array order.</summary>
    public BulkObservableCollection<TableRow> Rows { get; } = new();

    /// <summary>Adds a row.</summary>
    public ICommand AddCommand { get; }

    /// <summary>Removes the last row.</summary>
    public ICommand RemoveLastCommand { get; }

    private void BuildColumns()
    {
        List<TableColumn> columns = [];
        foreach (StructFieldType field in ElementType.GetFields())
        {
            if (field.GetProperty<InVisible>()?.Value == true) continue;

            LocaleString? display = field.GetProperty<Display>()?.Value;
            string header = display?.ToString(NodeImpl.Context) ?? field.Name;
            columns.Add(new TableColumn(field.Name, header, field.Require == true));
        }
        Columns.ReplaceAll(columns);
    }

    private void AddRow()
    {
        if (!IsEditable || IsDisposed) return;
        ArrayNodeImpl.AddRow();
    }

    private void RemoveLast()
    {
        if (!IsEditable || IsDisposed || ArrayNodeImpl.Count == 0) return;
        ArrayNodeImpl.DelRows(ArrayNodeImpl.Count - 1);
    }

    private void RemoveRow(TableRow row)
    {
        if (!IsEditable || IsDisposed) return;
        if (row.Index >= 0 && row.Index < ArrayNodeImpl.Count)
            ArrayNodeImpl.DelRows(row.Index);
    }

    private void OnArrayDataChanged((IValueAccess node, object? value) args)
    {
        if (_reconciling) return;
        Post(ReconcileRows);
    }

    private void ReconcileRows()
    {
        if (IsDisposed) return;
        _reconciling = true;
        try
        {
            TableColumn[] columns = Columns.ToArray();
            List<TableRow> next = [];
            HashSet<DataNode> alive = [];
            int index = 0;

            foreach (IValueAccess item in ArrayNodeImpl)
            {
                if (item is not DataNode element) continue;
                alive.Add(element);

                StructWrapper rowWrapper;
                if (_rowByNode.TryGetValue(element, out StructWrapper? cached))
                {
                    rowWrapper = cached;
                }
                else
                {
                    rowWrapper = (StructWrapper)NodeWrapperFactory.Create(element, Registry, null, SchemaViewRole.Cell);
                    _rowByNode[element] = rowWrapper;
                }

                TableRow row = new(index, rowWrapper, columns);
                rowWrapper.RemoveCommand = new RelayCommand(_ => RemoveRow(row), _ => IsEditable);
                next.Add(row);
                index++;
            }

            foreach (DataNode key in _rowByNode.Keys.Where(k => !alive.Contains(k)).ToList())
            {
                _rowByNode[key].Dispose();
                _rowByNode.Remove(key);
            }

            bool same = next.Count == Rows.Count;
            if (same)
            {
                for (int i = 0; i < next.Count; i++)
                {
                    if (Rows[i].Element != next[i].Element) { same = false; break; }
                    Rows[i].Index = next[i].Index;
                }
            }
            if (same) return;

            Rows.ReplaceAll(next);
        }
        finally
        {
            _reconciling = false;
        }
    }

    /// <inheritdoc />
    protected override void OnLanguageChanged()
    {
        Post(BuildColumns);
    }
}
