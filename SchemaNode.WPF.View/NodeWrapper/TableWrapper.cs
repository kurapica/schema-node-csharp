using System.Windows;
using System.Windows.Input;
using SchemaNode.Node;
using SchemaNode.Property.Array;
using SchemaNode.Property.Common;
using SchemaNode.Property.Struct;
using SchemaNode.Runtime;
using SchemaNode.Struct;
using SchemaNode.Utility;
using SchemaNode.WPF.View.Localization;
using SchemaNode.WPF.View.Views;

namespace SchemaNode.WPF.View.NodeWrapper;

/// <summary>
/// One projected table column. A group column mirrors vue's nested column: its header spans the
/// sub-columns, and an array group additionally owns a sub-operation column for add/delete links.
/// </summary>
public sealed class TableColumn
{
    internal TableColumn(
        string fieldName,
        string header,
        bool require,
        bool isArray,
        IReadOnlyList<TableColumn> subColumns,
        double width)
    {
        FieldName = fieldName;
        Header = header;
        Require = require;
        IsArray = isArray;
        SubColumns = subColumns;
        Width = width;
    }

    /// <summary>Schema field name of the top-level element field.</summary>
    public string FieldName { get; }

    /// <summary>Localized header text, including the unit suffix and the required marker.</summary>
    public string Header { get; }

    /// <summary>Whether the field is required (rendered with a marker).</summary>
    public bool Require { get; }

    /// <summary>Whether this group is backed by an array field (rendered as inline sub-rows).</summary>
    public bool IsArray { get; }

    /// <summary>Sub-columns when the field is a struct or a struct array; empty for a leaf.</summary>
    public IReadOnlyList<TableColumn> SubColumns { get; }

    /// <summary>Preferred width of a leaf column.</summary>
    public double Width { get; }

    /// <summary>Whether this column owns a two-level header.</summary>
    public bool IsGroup => SubColumns.Count > 0;

    /// <summary>Whether the column renders a single value cell per element.</summary>
    public bool IsLeaf => SubColumns.Count == 0;
}

/// <summary>One cell of the flattened header layout (top group row or leaf row).</summary>
public sealed class TableHeaderCell
{
    internal TableHeaderCell(string label, double width, ICommand? command = null, string? commandText = null)
    {
        Label = label;
        Width = width;
        Command = command;
        CommandText = commandText;
    }

    /// <summary>Localized header text.</summary>
    public string Label { get; }

    /// <summary>Total width of the header cell.</summary>
    public double Width { get; }

    /// <summary>Optional inline command (the add-row link on the operations header).</summary>
    public ICommand? Command { get; }

    /// <summary>Caption of the inline command.</summary>
    public string? CommandText { get; }

    /// <summary>Whether <see cref="Command"/> should be rendered.</summary>
    public bool HasCommand => Command is not null;
}

/// <summary>One flattened body column; array groups contribute one sub-operation column.</summary>
public sealed class TableLeafColumn
{
    internal TableLeafColumn(int index, TableColumn column, string fieldName, string header, double width, bool isSubOperation, bool isRowOperation)
    {
        Index = index;
        Column = column;
        FieldName = fieldName;
        Header = header;
        Width = width;
        IsSubOperation = isSubOperation;
        IsRowOperation = isRowOperation;
    }

    /// <summary>Zero-based layout column index.</summary>
    public int Index { get; }

    /// <summary>Owning top-level column.</summary>
    public TableColumn Column { get; }

    /// <summary>Leaf field name used to resolve the cell node.</summary>
    public string FieldName { get; }

    /// <summary>Localized leaf header text.</summary>
    public string Header { get; }

    /// <summary>Layout width.</summary>
    public double Width { get; }

    /// <summary>Whether this column holds add/delete links for an array group.</summary>
    public bool IsSubOperation { get; }

    /// <summary>Whether this column holds the row edit/delete links.</summary>
    public bool IsRowOperation { get; }
}

/// <summary>One clickable action rendered inside an operation cell.</summary>
public sealed class TableCellAction
{
    public TableCellAction(string label, ICommand? command)
    {
        Label = label;
        Command = command;
    }

    /// <summary>Localized button text.</summary>
    public string Label { get; }

    /// <summary>Command to execute; null when the action is unavailable.</summary>
    public ICommand? Command { get; }
}

/// <summary>What a body cell renders.</summary>
public enum TableCellKind
{
    /// <summary>A value cell backed by <see cref="TableCell.Wrapper"/>.</summary>
    Value,

    /// <summary>Add/delete links for an array group at one visual row.</summary>
    SubOperation,

    /// <summary>Edit/delete links for the element.</summary>
    RowOperation,
}

/// <summary>One positioned cell inside a row group.</summary>
public sealed class TableCell
{
    public TableCell(
        int columnIndex,
        int row,
        int rowSpan,
        TableCellKind kind,
        DataNodeWrapper? wrapper = null,
        IReadOnlyList<TableCellAction>? actions = null)
    {
        ColumnIndex = columnIndex;
        Row = row;
        RowSpan = rowSpan;
        Kind = kind;
        Wrapper = wrapper;
        Actions = actions ?? Array.Empty<TableCellAction>();
    }

    /// <summary>Flattened layout column index.</summary>
    public int ColumnIndex { get; }

    /// <summary>First visual row inside the element block.</summary>
    public int Row { get; }

    /// <summary>Number of visual rows this cell covers (rowspan).</summary>
    public int RowSpan { get; }

    /// <summary>Cell kind.</summary>
    public TableCellKind Kind { get; }

    /// <summary>Value wrapper for <see cref="TableCellKind.Value"/> cells.</summary>
    public DataNodeWrapper? Wrapper { get; }

    /// <summary>Actions for operation cells.</summary>
    public IReadOnlyList<TableCellAction> Actions { get; }
}

/// <summary>
/// One element of the table rendered as a block of visual rows. Non-array columns are single cells
/// spanning the whole block; array groups contribute one cell per inner element row, which is how
/// sibling cells are merged by rowspan for one element.
/// </summary>
public sealed class TableRowGroup
{
    internal TableRowGroup(
        int index,
        DataNode element,
        int visualRowCount,
        IReadOnlyList<double> columnWidths,
        IReadOnlyList<TableCell> cells,
        IReadOnlyList<DataNode>[] innerElements,
        int layoutVersion,
        bool isDeleted = false,
        string? cellSkin = null)
    {
        Index = index;
        Element = element;
        VisualRowCount = visualRowCount;
        ColumnWidths = columnWidths;
        Cells = cells;
        InnerElements = innerElements;
        LayoutVersion = layoutVersion;
        IsDeleted = isDeleted;
        CellSkin = cellSkin;
    }

    /// <summary>Zero-based element index in the array.</summary>
    public int Index { get; }

    /// <summary>Backing element node.</summary>
    public DataNode Element { get; }

    /// <summary>Number of visual rows this element occupies.</summary>
    public int VisualRowCount { get; }

    /// <summary>Layout column widths shared by every group.</summary>
    public IReadOnlyList<double> ColumnWidths { get; }

    /// <summary>Positioned cells.</summary>
    public IReadOnlyList<TableCell> Cells { get; }

    /// <summary>Inner element identities per array group, used for structural reuse.</summary>
    internal IReadOnlyList<DataNode>[] InnerElements { get; }

    /// <summary>Column-layout version this group was built for.</summary>
    internal int LayoutVersion { get; }

    /// <summary>
    /// Whether the host marked this element as deleted. Page tables use it to render the row grey
    /// while the deletion is still pending in the host tracker; plain tables always report false.
    /// </summary>
    public bool IsDeleted { get; }

    /// <summary>Skin the row's cell views resolve through; null means inherit from the table.</summary>
    public string? CellSkin { get; }
}

/// <summary>
/// Wraps an array of structs rendered as a table.
///
/// Columns follow the vue table: top-level fields become leaf columns, struct fields become a
/// two-level header group, and struct-array fields become an inline sub-table group with its own
/// add/delete column. The <see cref="ViewColumns"/> whitelist projects columns by field path
/// (including nested paths such as <c>discount.rate</c>); a non-empty whitelist switches the table
/// to row-editor mode, exactly like vue's <c>viewColumns</c> / <c>viewEdit</c> pair.
/// </summary>
public class TableWrapper : DataNodeWrapper
{
    private const double CellWidth = 180;
    private const double LocalStringWidth = 200;
    private const double OperationWidth = 100;

    private readonly Dictionary<DataNode, DataNodeWrapper> _cellByNode = [];
    private readonly Dictionary<DataNode, IDisposable> _innerSubscriptions = [];
    private readonly List<TableColumn> _topColumns = [];
    private readonly List<TableColumn> _arrayColumns = [];
    private readonly List<TableLeafColumn> _leafColumns = [];
    private string[] _viewColumns = [];
    private string? _cellSkin;
    private bool _tableReadOnly;
    private bool _reconciling;
    private bool _bulkUpdating;
    private int _layoutVersion;

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

        AddCommand = new RelayCommand(_ => AddRow(), _ => CanAddRow);

        BuildColumns();
        ReconcileRows();
        Track(node.Subscribe(OnArrayDataChanged, immediate: false));
    }

    /// <summary>Typed array node.</summary>
    public ArrayNode ArrayNodeImpl { get; }

    /// <summary>Struct element type used to derive columns.</summary>
    public StructType ElementType { get; }

    /// <summary>Top-level projected columns, in schema order.</summary>
    public BulkObservableCollection<TableColumn> Columns { get; } = new();

    /// <summary>Flattened header cells of the first (group) header row.</summary>
    public BulkObservableCollection<TableHeaderCell> TopHeaders { get; } = new();

    /// <summary>Flattened header cells of the leaf header row.</summary>
    public BulkObservableCollection<TableHeaderCell> LeafHeaders { get; } = new();

    /// <summary>Element blocks, in array order.</summary>
    public BulkObservableCollection<TableRowGroup> RowGroups { get; } = new();

    /// <summary>Adds a row. In row-editor mode the editor opens first and the row is inserted on confirm.</summary>
    public ICommand AddCommand { get; }

    /// <summary>Window used to own the built-in row editor; set by the table view when it loads.</summary>
    public Window? OwnerWindow { get; set; }

    /// <summary>
    /// Skin the cell views resolve through. Null keeps the skin the cells inherit from the table,
    /// which is what the surrounding form uses. Changing it rebuilds the row groups (and therefore
    /// the cell views) but reuses the cached cell wrappers.
    /// </summary>
    public string? CellSkin
    {
        get => _cellSkin;
        set
        {
            string? normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (string.Equals(_cellSkin, normalized, StringComparison.OrdinalIgnoreCase)) return;

            _cellSkin = normalized;
            RaisePropertyChanged(nameof(CellSkin));
            RunOnUiThread(ReconcileRows);
        }
    }

    /// <summary>
    /// Skin forwarded to the built-in row editor. A separate window does not inherit dependency
    /// property values, so the table hands its effective skin to the editor explicitly.
    /// </summary>
    public string EditorSkin { get; set; } = SchemaViewRegistry.DefaultSkin;

    /// <summary>
    /// Field-path whitelist. An empty list keeps the derived column set. A non-empty list renders
    /// only matching columns and switches cells to read-only row-editor mode.
    /// </summary>
    public IReadOnlyList<string> ViewColumns
    {
        get => _viewColumns;
        set
        {
            string[] normalized = value?
                .Select(v => v?.Trim())
                .Where(v => !string.IsNullOrEmpty(v))
                .Select(v => v!)
                .Distinct(StringComparer.Ordinal)
                .ToArray() ?? [];
            if (_viewColumns.SequenceEqual(normalized)) return;

            _viewColumns = normalized;
            RaisePropertyChanged(nameof(ViewColumns));
            RunOnUiThread(RebuildAll);
        }
    }

    /// <summary>
    /// Explicitly renders the table read-only. Cell editors are disabled and add/delete actions
    /// disappear, while a row editor (when enabled by <see cref="ViewColumns"/>) stays available.
    /// </summary>
    public bool ReadOnly
    {
        get => _tableReadOnly;
        set
        {
            if (_tableReadOnly == value) return;
            _tableReadOnly = value;
            RaisePropertyChanged(nameof(ReadOnly));
            RunOnUiThread(RebuildAll);
        }
    }

    /// <summary>Whether columns are projected through a whitelist and cells are edited per row.</summary>
    public bool RowEditorMode => _viewColumns.Length > 0;

    /// <summary>Whether the table has at least one two-level header group.</summary>
    public bool HasGroupHeaders => _topColumns.Any(c => c.IsGroup);

    /// <summary>Whether the operations column and its header are rendered.</summary>
    public bool ShowOperations => RowEditorMode || CanAddRow || CanDeleteRow;

    /// <summary>Whether a row can be added (editor mode included).</summary>
    public bool CanAddRow => !IsDisposed && !_tableReadOnly && IsEditable && !HasMaxSize();

    /// <summary>Whether rows can be deleted.</summary>
    public virtual bool CanDeleteRow => !IsDisposed && !_tableReadOnly && IsEditable && ArrayNodeImpl.Count > 0 && !HasMinSize();

    private bool IsCellReadOnly => _tableReadOnly || RowEditorMode;

    /// <summary>Whether row mutations (add/delete) are allowed by the current table state.</summary>
    protected bool CanMutateRows => !IsDisposed && !_tableReadOnly && IsEditable;

    #region Columns

    private void BuildColumns()
    {
        _topColumns.Clear();
        _arrayColumns.Clear();

        foreach (StructFieldType field in ElementType.GetFields())
        {
            if (field.GetProperty<InVisible>()?.Value == true) continue;
            if (!AllowsTopField(field.Name)) continue;

            TableColumn column = BuildColumn(field);
            _topColumns.Add(column);
            if (column.IsArray) _arrayColumns.Add(column);
        }

        Columns.ReplaceAll(_topColumns);
        BuildHeaderLayout();
        _layoutVersion++;
    }

    private TableColumn BuildColumn(StructFieldType field)
    {
        string header = FormatHeader(field);
        bool require = field.Require == true;
        double width = WidthFor(field.Type);

        if (field.Type is ArrayType arrayType && arrayType.Element is StructType arrayElement
            && Registry.ShouldUseTable(arrayType))
        {
            IReadOnlyList<TableColumn> subColumns = BuildSubColumns(arrayElement, field.Name);
            return new TableColumn(field.Name, header, require, isArray: true, subColumns, width);
        }

        if (field.Type is StructType structType && !Registry.UseSingleView(structType))
        {
            IReadOnlyList<TableColumn> subColumns = BuildSubColumns(structType, field.Name);
            return new TableColumn(field.Name, header, require, isArray: false, subColumns, width);
        }

        return new TableColumn(field.Name, header, require, isArray: false, Array.Empty<TableColumn>(), width);
    }

    /// <summary>
    /// Builds one level of sub-columns. Deeper nesting is a leaf rendered by its own view, which
    /// matches vue's <c>genColumn(..., skipSub = true)</c> for sub-columns.
    /// </summary>
    private IReadOnlyList<TableColumn> BuildSubColumns(StructType type, string parentField)
    {
        List<TableColumn> subs = [];
        bool wholeGroupSelected = _viewColumns.Length > 0 && _viewColumns.Contains(parentField, StringComparer.Ordinal);

        foreach (StructFieldType field in type.GetFields())
        {
            if (field.GetProperty<DisplayOnly>()?.Value == true) continue;
            if (field.GetProperty<InVisible>()?.Value == true) continue;

            if (_viewColumns.Length > 0 && !wholeGroupSelected
                && !_viewColumns.Contains($"{parentField}.{field.Name}", StringComparer.Ordinal))
                continue;

            subs.Add(new TableColumn(
                field.Name,
                FormatHeader(field),
                field.Require == true,
                isArray: false,
                Array.Empty<TableColumn>(),
                WidthFor(field.Type)));
        }

        return subs;
    }

    private bool AllowsTopField(string fieldName)
    {
        if (_viewColumns.Length == 0) return true;
        return _viewColumns.Any(path =>
            string.Equals(path, fieldName, StringComparison.Ordinal)
            || path.StartsWith(fieldName + ".", StringComparison.Ordinal));
    }

    private void BuildHeaderLayout()
    {
        _leafColumns.Clear();
        List<TableHeaderCell> topHeaders = [];
        List<TableHeaderCell> leafHeaders = [];

        foreach (TableColumn column in _topColumns)
        {
            if (!column.IsGroup)
            {
                TableLeafColumn leaf = new(_leafColumns.Count, column, column.FieldName, column.Header, column.Width, false, false);
                _leafColumns.Add(leaf);
                topHeaders.Add(new TableHeaderCell(string.Empty, leaf.Width));
                leafHeaders.Add(new TableHeaderCell(leaf.Header, leaf.Width));
                continue;
            }

            List<TableLeafColumn> subLeaves = [];
            foreach (TableColumn sub in column.SubColumns)
            {
                TableLeafColumn leaf = new(_leafColumns.Count, column, sub.FieldName, sub.Header, sub.Width, false, false);
                _leafColumns.Add(leaf);
                subLeaves.Add(leaf);
            }

            // vue omits the sub-operation column entirely when the table cannot mutate rows, so a
            // read-only table does not render an empty action column next to the sub-table.
            if (column.IsArray && CanMutateRows)
            {
                TableLeafColumn leaf = new(_leafColumns.Count, column, string.Empty, string.Empty, OperationWidth, true, false);
                _leafColumns.Add(leaf);
                subLeaves.Add(leaf);
            }

            double groupWidth = subLeaves.Sum(l => l.Width);
            topHeaders.Add(new TableHeaderCell(column.Header, groupWidth));
            leafHeaders.AddRange(subLeaves.Select(l => new TableHeaderCell(l.Header, l.Width)));
        }

        if (ShowOperations)
        {
            TableLeafColumn leaf = new(_leafColumns.Count, new TableColumn(string.Empty, string.Empty, false, false, [], OperationWidth),
                string.Empty, string.Empty, OperationWidth, false, true);
            _leafColumns.Add(leaf);
            topHeaders.Add(new TableHeaderCell(string.Empty, OperationWidth));
            leafHeaders.Add(new TableHeaderCell(
                LocaleManager.Instance.Get("wpf.table.operations"),
                OperationWidth,
                CanAddRow ? AddCommand : null,
                CanAddRow ? "+" : null));
        }

        TopHeaders.ReplaceAll(topHeaders);
        LeafHeaders.ReplaceAll(leafHeaders);
    }

    #endregion

    #region Rows

    private void ReconcileRows()
    {
        if (IsDisposed) return;

        _reconciling = true;
        try
        {
            TableLeafColumn[] leaves = _leafColumns.ToArray();
            List<TableRowGroup> next = [];
            HashSet<DataNode> alive = [];
            HashSet<DataNode> aliveArrays = [];
            int index = 0;

            foreach (IValueAccess item in ArrayNodeImpl)
            {
                if (item is not DataNode element) continue;
                alive.Add(element);

                TableRowGroup group = BuildRowGroup(index, element, leaves, alive, aliveArrays);
                next.Add(group);
                index++;
            }

            foreach (DataNode key in _cellByNode.Keys.Where(k => !alive.Contains(k)).ToList())
            {
                _cellByNode[key].Dispose();
                _cellByNode.Remove(key);
            }

            foreach (DataNode key in _innerSubscriptions.Keys.Where(k => !aliveArrays.Contains(k)).ToList())
            {
                _innerSubscriptions[key].Dispose();
                _innerSubscriptions.Remove(key);
            }

            if (IsSameStructure(next))
                return;

            RowGroups.ReplaceAll(next);
        }
        finally
        {
            _reconciling = false;
        }

        RaiseStateChanged();
    }

    /// <summary>
    /// Keeps the existing visual blocks when the element identity set, column layout and inner
    /// array element set are unchanged. Without this a scalar edit inside a cell would replace
    /// every row block on each keystroke and drop keyboard focus.
    /// </summary>
    private bool IsSameStructure(List<TableRowGroup> next)
    {
        if (RowGroups.Count != next.Count) return false;

        for (int i = 0; i < next.Count; i++)
        {
            TableRowGroup current = RowGroups[i];
            TableRowGroup candidate = next[i];

            if (!ReferenceEquals(current.Element, candidate.Element)) return false;
            if (current.IsDeleted != candidate.IsDeleted) return false;
            if (!string.Equals(current.CellSkin, candidate.CellSkin, StringComparison.OrdinalIgnoreCase)) return false;
            if (current.LayoutVersion != candidate.LayoutVersion) return false;
            if (current.VisualRowCount != candidate.VisualRowCount) return false;
            if (current.InnerElements.Length != candidate.InnerElements.Length) return false;

            for (int g = 0; g < current.InnerElements.Length; g++)
            {
                IReadOnlyList<DataNode> left = current.InnerElements[g];
                IReadOnlyList<DataNode> right = candidate.InnerElements[g];
                if (left.Count != right.Count) return false;
                for (int r = 0; r < left.Count; r++)
                    if (!ReferenceEquals(left[r], right[r])) return false;
            }
        }

        return true;
    }

    private TableRowGroup BuildRowGroup(
        int index,
        DataNode element,
        TableLeafColumn[] leaves,
        HashSet<DataNode> alive,
        HashSet<DataNode> aliveArrays)
    {
        Dictionary<string, ArrayNode?> innerByField = [];
        List<IReadOnlyList<DataNode>> innerElements = [];
        int maxLength = 0;
        bool anyAddable = false;

        foreach (TableColumn group in _arrayColumns)
        {
            ArrayNode? inner = GetInnerArray(element, group.FieldName);
            innerByField[group.FieldName] = inner;

            List<DataNode> elements = [];
            if (inner is not null)
            {
                aliveArrays.Add(inner);
                EnsureInnerSubscription(inner);
                foreach (IValueAccess item in inner)
                    if (item is DataNode innerNode) elements.Add(innerNode);

                maxLength = Math.Max(maxLength, elements.Count);
                anyAddable |= CanAddInner(inner);
            }

            innerElements.Add(elements);
        }

        int visualRows = maxLength + (anyAddable ? 1 : 0);
        if (visualRows < 1) visualRows = 1;

        List<TableCell> cells = [];
        foreach (TableLeafColumn leaf in leaves)
        {
            if (leaf.IsRowOperation)
            {
                cells.Add(BuildRowOperationCell(element, leaf.Index, visualRows));
                continue;
            }

            if (leaf.IsSubOperation)
            {
                ArrayNode? inner = innerByField.GetValueOrDefault(leaf.Column.FieldName);
                AddSubOperationCells(cells, leaf, inner, visualRows);
                continue;
            }

            if (leaf.Column.IsArray)
            {
                ArrayNode? inner = innerByField.GetValueOrDefault(leaf.Column.FieldName);
                int length = inner?.Count ?? 0;
                for (int row = 0; row < visualRows; row++)
                {
                    DataNodeWrapper? wrapper = null;
                    if (row < length)
                    {
                        DataNode? node = ResolveArraySubNode(inner!, leaf.FieldName, row);
                        if (node is not null)
                        {
                            alive.Add(node);
                            wrapper = GetCellWrapper(node, leaf.FieldName);
                        }
                    }
                    cells.Add(new TableCell(leaf.Index, row, 1, TableCellKind.Value, wrapper));
                }
                continue;
            }

            DataNode? valueNode = ResolveColumnNode(element, leaf);
            DataNodeWrapper? value = null;
            if (valueNode is not null)
            {
                alive.Add(valueNode);
                value = GetCellWrapper(valueNode, leaf.FieldName);
            }
            cells.Add(new TableCell(leaf.Index, 0, visualRows, TableCellKind.Value, value));
        }

        return new TableRowGroup(
            index,
            element,
            visualRows,
            _leafColumns.Select(l => l.Width).ToArray(),
            cells,
            innerElements.ToArray(),
            _layoutVersion,
            IsRowDeleted(element),
            _cellSkin);
    }

    /// <summary>
    /// Whether the host marked an element as deleted. Page tables override this to render the
    /// pending-delete state; the base table never does.
    /// </summary>
    protected virtual bool IsRowDeleted(DataNode element) => false;

    private void AddSubOperationCells(List<TableCell> cells, TableLeafColumn leaf, ArrayNode? inner, int visualRows)
    {
        for (int row = 0; row < visualRows; row++)
        {
            List<TableCellAction> actions = [];
            if (inner is not null)
            {
                if (row == inner.Count)
                {
                    if (CanAddInner(inner))
                        actions.Add(new TableCellAction(
                            LocaleManager.Instance.Get("wpf.button.add"),
                            new RelayCommand(_ => AddInnerRow(inner), _ => CanAddInner(inner))));
                }
                else if (row < inner.Count && CanDeleteInner(inner))
                {
                    DataNode? target = inner[row] as DataNode;
                    actions.Add(new TableCellAction(
                        LocaleManager.Instance.Get("wpf.button.delete"),
                        new RelayCommand(
                            _ =>
                            {
                                int position = target is null ? -1 : inner.IndexOf(target);
                                if (position >= 0 && CanDeleteInner(inner)) inner.DelRows(position);
                            },
                            _ => target is not null && CanDeleteInner(inner))));
                }
            }

            cells.Add(new TableCell(leaf.Index, row, 1, TableCellKind.SubOperation, null, actions));
        }
    }

    /// <summary>Builds the trailing element operations cell; page tables extend it with resume.</summary>
    protected virtual TableCell BuildRowOperationCell(DataNode element, int columnIndex, int visualRows)
    {
        List<TableCellAction> actions = [];
        if (RowEditorMode)
        {
            actions.Add(new TableCellAction(
                LocaleManager.Instance.Get("wpf.table.button.edit"),
                new RelayCommand(_ => EditRow(element), _ => !IsDisposed)));
        }

        if (CanDeleteRow)
        {
            actions.Add(new TableCellAction(
                LocaleManager.Instance.Get("wpf.button.delete"),
                new RelayCommand(_ => DeleteRow(element), _ => CanDeleteRow)));
        }

        return new TableCell(columnIndex, 0, visualRows, TableCellKind.RowOperation, null, actions);
    }

    private DataNode? ResolveColumnNode(DataNode element, TableLeafColumn leaf)
    {
        IValueAccess? value = element.GetAccessValue(leaf.Column.FieldName);
        if (leaf.Column.IsGroup && value is not null)
            value = value.GetAccessValue(leaf.FieldName);
        return value as DataNode;
    }

    private static DataNode? ResolveArraySubNode(ArrayNode inner, string fieldName, int row)
    {
        if (row < 0 || row >= inner.Count) return null;
        return inner[row] is IValueAccess element ? element.GetAccessValue(fieldName) as DataNode : null;
    }

    private DataNodeWrapper GetCellWrapper(DataNode node, string fieldName)
    {
        DataNodeWrapper wrapper = _cellByNode.TryGetValue(node, out DataNodeWrapper? existing)
            ? existing
            : NodeWrapperFactory.Create(node, Registry, fieldName, SchemaViewRole.Cell);

        _cellByNode[node] = wrapper;
        wrapper.SetForcedReadOnly(IsCellReadOnly);
        return wrapper;
    }

    private ArrayNode? GetInnerArray(DataNode element, string fieldName)
        => element.GetAccessValue(fieldName) as ArrayNode;

    private void EnsureInnerSubscription(ArrayNode inner)
    {
        if (_innerSubscriptions.ContainsKey(inner)) return;
        _innerSubscriptions[inner] = inner.Subscribe(OnInnerArrayChanged, immediate: false);
    }

    private void OnInnerArrayChanged((IValueAccess node, object? value) args)
    {
        if (_reconciling || _bulkUpdating) return;
        Post(ReconcileRows);
    }

    #endregion

    #region Mutations

    private void AddRow()
    {
        if (!CanAddRow) return;

        if (!RowEditorMode)
        {
            ArrayNodeImpl.AddRow();
            return;
        }

        InsertWithEditor(ArrayNodeImpl);
    }

    private void AddInnerRow(ArrayNode inner)
    {
        if (!CanAddInner(inner)) return;

        if (!RowEditorMode)
        {
            inner.AddRow();
            return;
        }

        InsertWithEditor(inner);
    }

    /// <summary>
    /// Creates a detached element, edits it through the row editor and only inserts it when the
    /// user confirms. Cancelling disposes the node so nothing leaks into the array.
    /// </summary>
    private void InsertWithEditor(ArrayNode array)
    {
        IValueAccess? created = array.ElementType.Create(NodeImpl.Context, null, array);
        if (created is not DataNode node) return;

        if (ShowRowEditor(node, isNew: true))
        {
            if (array.AddRow(null, node) is null) node.Dispose();
        }
        else
        {
            node.Dispose();
        }
    }

    /// <summary>Opens the row editor for an existing element.</summary>
    protected void EditRow(DataNode element)
    {
        if (IsDisposed) return;
        ShowRowEditor(element, isNew: false);
    }

    /// <summary>Deletes one element; page tables override it to record a pending host deletion.</summary>
    protected virtual void DeleteRow(DataNode element)
    {
        if (!CanDeleteRow) return;
        int index = ArrayNodeImpl.IndexOf(element);
        if (index >= 0) ArrayNodeImpl.DelRows(index);
    }

    private bool ShowRowEditor(DataNode node, bool isNew)
    {
        string title = LocaleManager.Instance.Get(
            isNew ? "wpf.table.title.new" : "wpf.table.title.editor");
        // A separate window does not inherit the table's dependency property values, so the skin
        // is carried in the request instead of silently falling back to the default skin.
        TableRowEditorRequest request = new(node, isNew, title, OwnerWindow, Registry, EditorSkin);
        Func<TableRowEditorRequest, bool> editor = Registry.RowEditorFactory ?? TableRowEditorWindow.Show;
        return editor(request);
    }

    private bool CanAddInner(ArrayNode inner)
        => CanMutateRows && !HasMaxSize(inner);

    private bool CanDeleteInner(ArrayNode inner)
        => CanMutateRows && inner.Count > 0 && !HasMinSize(inner);

    private bool HasMaxSize() => HasMaxSize(ArrayNodeImpl);

    private bool HasMinSize() => HasMinSize(ArrayNodeImpl);

    private static bool HasMaxSize(ArrayNode array)
        => array.GetProperty<MaxSize>() is { HasValue: true } property && array.Count >= property.Value;

    private static bool HasMinSize(ArrayNode array)
        => array.GetProperty<MinSize>() is { HasValue: true } property && array.Count <= property.Value;

    #endregion

    #region Helpers

    /// <summary>Builds the localized leaf header text (display, required marker and unit).</summary>
    protected string FormatHeader(StructFieldType field)
    {
        LocaleString? display = field.GetProperty<Display>()?.Value;
        string label = display?.ToString(NodeImpl.Context) ?? field.Name;

        LocaleString? unit = field.GetProperty<Unit>()?.Value ?? field.Type?.GetProperty<Unit>()?.Value;
        if (unit is not null)
            label = $"{label}({unit.ToString(NodeImpl.Context)})";

        if (field.Require == true)
            label += " *";

        return label;
    }

    private static double WidthFor(IValueTypeAccess? type)
    {
        IValueTypeAccess? valueType = type is ArrayType { Element: not null } array ? array.Element : type;
        return valueType?.Name == Constant.NS_SYSTEM_LOCALE_STRING ? LocalStringWidth : CellWidth;
    }

    private void ApplyForcedReadOnly()
    {
        bool forced = IsCellReadOnly;
        foreach (DataNodeWrapper wrapper in _cellByNode.Values)
            wrapper.SetForcedReadOnly(forced);
    }

    /// <summary>Rebuilds columns and rows and refreshes the derived state notifications.</summary>
    protected void RebuildAll()
    {
        if (IsDisposed) return;
        ApplyForcedReadOnly();
        BuildColumns();
        ReconcileRows();
        RaiseStateChanged();
    }

    /// <summary>Raises change notifications for the state derived from the table contents.</summary>
    protected void RaiseStateChanged()
    {
        RaisePropertyChanged(nameof(RowEditorMode));
        RaisePropertyChanged(nameof(HasGroupHeaders));
        RaisePropertyChanged(nameof(ShowOperations));
        RaisePropertyChanged(nameof(CanAddRow));
        RaisePropertyChanged(nameof(CanDeleteRow));
        (AddCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    /// <summary>Runs UI-bound code on the dispatcher thread.</summary>
    protected void RunOnUiThread(Action action)
    {
        if (Dispatcher.CheckAccess()) action();
        else Post(action);
    }

    /// <summary>
    /// Applies a batch of element replacements and reconciles once at the end. Notifications fired
    /// by the individual array mutations are suppressed so a page swap does not rebuild the table
    /// for every row.
    /// </summary>
    protected void RunBulkUpdate(Action action)
    {
        if (IsDisposed) return;

        _bulkUpdating = true;
        try
        {
            action();
        }
        finally
        {
            _bulkUpdating = false;
        }

        ReconcileRows();
    }

    private void OnArrayDataChanged((IValueAccess node, object? value) args)
    {
        if (_reconciling || _bulkUpdating) return;
        Post(ReconcileRows);
    }

    #endregion

    /// <inheritdoc />
    protected override void OnForcedReadOnlyChanged(bool value)
    {
        foreach (DataNodeWrapper wrapper in _cellByNode.Values)
            wrapper.SetForcedReadOnly(value);
    }

    /// <inheritdoc />
    protected override void OnEditableChanged() => Post(RebuildAll);

    /// <inheritdoc />
    protected override void OnLanguageChanged() => Post(RebuildAll);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !IsDisposed)
        {
            foreach (IDisposable subscription in _innerSubscriptions.Values)
                subscription.Dispose();
            _innerSubscriptions.Clear();

            foreach (DataNodeWrapper wrapper in _cellByNode.Values)
                wrapper.Dispose();
            _cellByNode.Clear();

            RowGroups.ReplaceAll([]);
        }
        base.Dispose(disposing);
    }
}
