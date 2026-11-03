using SchemaNode.Node;
using SchemaNode.Runtime;

namespace SchemaNode.WPF.View.NodeWrapper;

/// <summary>
/// Wraps a struct node and owns one child wrapper per field.
///
/// Child wrappers are reconciled by node identity: a value change keeps the same instances,
/// only schema changes (for example an Attach-driven field set switch) add or drop wrappers.
/// </summary>
public sealed class StructWrapper : DataNodeWrapper
{
    private readonly Dictionary<DataNode, DataNodeWrapper> _childByNode = [];
    private bool _reconciling;

    /// <inheritdoc />
    public override string? DefaultTemplateKey => SchemaTemplateKeys.Struct;

    public StructWrapper(DataNode node, SchemaViewRegistry? registry, string? fieldName, SchemaViewRole role)
        : base(node, registry, fieldName, role)
    {
        StructNodeImpl = node as StructNode
            ?? throw new ArgumentException($"Node '{node.Type.Name}' is not a struct node.", nameof(node));

        ReconcileChildren();
        Track(node.Subscribe(OnStructDataChanged, immediate: false));
    }

    /// <summary>Typed struct node.</summary>
    public StructNode StructNodeImpl { get; }

    /// <summary>Field wrappers in schema order.</summary>
    public BulkObservableCollection<DataNodeWrapper> Children { get; } = new();

    /// <summary>Finds a field wrapper by schema field name.</summary>
    public DataNodeWrapper? GetChild(string fieldName)
        => Children.FirstOrDefault(c => string.Equals(c.FieldName, fieldName, StringComparison.Ordinal));

    private void OnStructDataChanged((IValueAccess node, object? value) args)
    {
        if (_reconciling) return;
        // A struct value write does not change the field set; only reconcile when the schema
        // (the set of field nodes) actually changed.
        Post(ReconcileChildren);
    }

    private void ReconcileChildren()
    {
        if (IsDisposed) return;
        _reconciling = true;
        try
        {
            List<(StructFieldType Field, DataNodeWrapper Wrapper)> desired = [];
            HashSet<DataNode> alive = [];
            foreach ((StructFieldType field, IValueAccess value) in StructNodeImpl.GetFields())
            {
                if (value is not DataNode child) continue;
                alive.Add(child);

                DataNodeWrapper wrapper = _childByNode.TryGetValue(child, out DataNodeWrapper? existing)
                    ? existing
                    : NodeWrapperFactory.Create(child, Registry, field.Name, Role);
                _childByNode[child] = wrapper;
                desired.Add((field, wrapper));
            }

            foreach (DataNode key in _childByNode.Keys.Where(k => !alive.Contains(k)).ToList())
            {
                _childByNode[key].Dispose();
                _childByNode.Remove(key);
            }

            List<DataNodeWrapper> next = desired.Select(d => d.Wrapper).ToList();
            if (next.Count == Children.Count && next.SequenceEqual(Children))
                return;

            Children.ReplaceAll(next);
        }
        finally
        {
            _reconciling = false;
        }
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
