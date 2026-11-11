using System.Windows.Input;
using SchemaNode.Node;
using SchemaNode.Runtime;

namespace SchemaNode.Avalonia.View.NodeWrapper;

/// <summary>
/// Wraps an array node and owns one child wrapper per element. Membership is reconciled by
/// element node identity, so editing a value inside an element never rebuilds the list.
/// </summary>
public sealed class ArrayWrapper : DataNodeWrapper
{
    private readonly Dictionary<DataNode, DataNodeWrapper> _childByNode = [];
    private bool _reconciling;

    /// <inheritdoc />
    public override string? DefaultTemplateKey => SchemaTemplateKeys.Array;

    /// <summary>An array only groups elements: the failing leaf shows the message.</summary>
    protected override bool RendersValue => false;

    public ArrayWrapper(DataNode node, SchemaViewRegistry? registry, string? fieldName, SchemaViewRole role)
        : base(node, registry, fieldName, role)
    {
        // Editable arrays derive from ArrayNode. Specialized list nodes only expose their elements,
        // so they render read-only through the same view and commands stay unavailable.
        ArrayNodeImpl = node as ArrayNode;
        if (ArrayNodeImpl is null && node is not IEnumerable<IValueAccess>)
            throw new ArgumentException($"Node '{node.Type.Name}' is not an array node.", nameof(node));

        AddCommand = ArrayNodeImpl is null ? null : new RelayCommand(_ => AddRow(), _ => IsEditable);
        RemoveLastCommand = ArrayNodeImpl is null
            ? null
            : new RelayCommand(_ => RemoveLast(), _ => IsEditable && Children.Count > 0);

        ReconcileChildren();
        RefreshCommandStates();
        Track(node.Subscribe(OnArrayDataChanged, immediate: false));
    }

    /// <summary>Typed array node, null when the node is a read-only list.</summary>
    public ArrayNode? ArrayNodeImpl { get; }

    /// <summary>Element wrappers in array order.</summary>
    public BulkObservableCollection<DataNodeWrapper> Children { get; } = new();

    /// <summary>Adds an element at the end of the array; null for a read-only list.</summary>
    public ICommand? AddCommand { get; }

    /// <summary>Removes the last element; null for a read-only list.</summary>
    public ICommand? RemoveLastCommand { get; }

    private void OnArrayDataChanged((IValueAccess node, object? value) args)
    {
        if (_reconciling) return;
        Post(ReconcileChildren);
    }

    private void AddRow()
    {
        if (!IsEditable || IsDisposed || ArrayNodeImpl is null) return;
        ArrayNodeImpl.AddRow();
    }

    private void RemoveLast()
    {
        if (!IsEditable || IsDisposed || ArrayNodeImpl is null || ArrayNodeImpl.Count == 0) return;
        if (ArrayNodeImpl[ArrayNodeImpl.Count - 1] is DataNode element)
            DeleteElement(element);
        else
            ArrayNodeImpl.DelRows(ArrayNodeImpl.Count - 1);
    }

    private void RemoveChild(DataNodeWrapper child)
    {
        if (!IsEditable || IsDisposed || ArrayNodeImpl is null) return;
        DeleteElement(child.Node);
    }

    /// <summary>
    /// Removes the element from the array and releases both its wrapper and its node. DelRows only
    /// removes the array entry, so the deleted subtree would otherwise stay subscribed.
    /// </summary>
    private void DeleteElement(DataNode element)
    {
        if (ArrayNodeImpl is null) return;

        int index = ArrayNodeImpl.IndexOf(element);
        if (index < 0) return;

        ArrayNodeImpl.DelRows(index);

        if (_childByNode.Remove(element, out DataNodeWrapper? wrapper))
            wrapper.Dispose();

        element.Dispose();
    }

    private void ReconcileChildren()
    {
        if (IsDisposed) return;
        _reconciling = true;
        try
        {
            List<DataNodeWrapper> next = [];
            HashSet<DataNode> alive = [];

            IEnumerable<IValueAccess> items = ArrayNodeImpl ?? (IEnumerable<IValueAccess>)NodeImpl;
            foreach (IValueAccess item in items)
            {
                if (item is not DataNode element) continue;
                alive.Add(element);

                DataNodeWrapper wrapper;
                if (_childByNode.TryGetValue(element, out DataNodeWrapper? existing))
                {
                    wrapper = existing;
                }
                else
                {
                    wrapper = NodeWrapperFactory.Create(element, Registry, null, Role);
                    // The command reads IsEditable dynamically, so it can be created once and simply
                    // re-queried when editability changes; recreating it would leave the bound button
                    // pointing at a stale instance that receives no notifications.
                    wrapper.RemoveCommand = ArrayNodeImpl is null
                        ? null
                        : new RelayCommand(_ => RemoveChild(wrapper), _ => IsEditable);
                }
                wrapper.SetForcedReadOnly(IsForcedReadOnly);
                _childByNode[element] = wrapper;
                next.Add(wrapper);
            }

            foreach (DataNode key in _childByNode.Keys.Where(k => !alive.Contains(k)).ToList())
            {
                _childByNode[key].Dispose();
                _childByNode.Remove(key);
            }

            if (next.Count == Children.Count && next.SequenceEqual(Children))
                return;

            Children.ReplaceAll(next);
            RefreshCommandStates();
        }
        finally
        {
            _reconciling = false;
        }
    }

    /// <summary>
    /// Re-queries the add/remove commands. The row count drives remove-last and editability drives
    /// every command, so both need an explicit refresh when they change. Avalonia has no
    /// CommandManager equivalent, so the reload notification has to be raised by hand.
    /// </summary>
    private void RefreshCommandStates()
    {
        if (IsDisposed) return;

        (AddCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RemoveLastCommand as RelayCommand)?.RaiseCanExecuteChanged();
        foreach (DataNodeWrapper child in Children)
            (child.RemoveCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    /// <inheritdoc />
    protected override void OnEditableChanged() => RefreshCommandStates();

    /// <inheritdoc />
    protected override void OnForcedReadOnlyChanged(bool value)
    {
        foreach (DataNodeWrapper child in Children)
            child.SetForcedReadOnly(value);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !IsDisposed)
        {
            foreach (DataNodeWrapper child in Children)
                child.Dispose();
            Children.ReplaceAll([]);
            _childByNode.Clear();
        }
        base.Dispose(disposing);
    }
}
