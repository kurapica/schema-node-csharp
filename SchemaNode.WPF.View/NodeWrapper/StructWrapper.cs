using System.ComponentModel;
using SchemaNode.Node;
using SchemaNode.Property.Struct;
using SchemaNode.Runtime;

namespace SchemaNode.WPF.View.NodeWrapper;

/// <summary>
/// Wraps a struct node and owns one child wrapper per field.
///
/// Child wrappers are reconciled by node identity: a value change keeps the same instances,
/// only schema changes (for example an Attach-driven field set switch) add or drop wrappers.
///
/// <see cref="Children"/> is the materialized set: a field whose node is invisible when the
/// struct is first shown is kept out of the collection, so the ItemsControl never creates its
/// view. The wrapper is still created (identity, read-only propagation, visibility subscription);
/// once the node becomes visible it is inserted at its schema position. A field that was already
/// shown is never removed on a later hide — the row's own IsVisible binding hides it.
/// </summary>
public sealed class StructWrapper : DataNodeWrapper
{
    private readonly Dictionary<DataNode, DataNodeWrapper> _childByNode = [];
    private readonly Dictionary<DataNodeWrapper, PropertyChangedEventHandler> _visibilityHandlers = [];
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

    /// <summary>Materialized field wrappers in schema order.</summary>
    public BulkObservableCollection<DataNodeWrapper> Children { get; } = new();

    /// <summary>Finds a field wrapper by schema field name (materialized or not).</summary>
    public DataNodeWrapper? GetChild(string fieldName)
        => _childByNode.Values.FirstOrDefault(c => string.Equals(c.FieldName, fieldName, StringComparison.Ordinal));

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
            List<DataNodeWrapper> next = [];
            HashSet<DataNode> alive = [];
            foreach ((StructFieldType field, IValueAccess value) in StructNodeImpl.GetFields())
            {
                if (value is not DataNode child) continue;
                alive.Add(child);

                DataNodeWrapper wrapper = _childByNode.TryGetValue(child, out DataNodeWrapper? existing)
                    ? existing
                    : NodeWrapperFactory.Create(child, Registry, field.Name, Role);
                wrapper.SetForcedReadOnly(IsForcedReadOnly);
                bool known = _childByNode.ContainsKey(child);
                _childByNode[child] = wrapper;

                // A known wrapper is materialized only at creation time or by its visibility
                // callback; a struct value write must never re-add it through a reset.
                if (known ? Children.Contains(wrapper) : wrapper.IsVisible)
                {
                    UntrackVisibility(wrapper);
                    next.Add(wrapper);
                }
                else
                {
                    TrackVisibility(wrapper);
                }
            }

            foreach (DataNode key in _childByNode.Keys.Where(k => !alive.Contains(k)).ToList())
            {
                DataNodeWrapper wrapper = _childByNode[key];
                UntrackVisibility(wrapper);
                wrapper.Dispose();
                _childByNode.Remove(key);
            }

            if (next.Count == Children.Count && next.SequenceEqual(Children))
                return;

            Children.ReplaceAll(next);
        }
        finally
        {
            _reconciling = false;
        }
    }

    private void TrackVisibility(DataNodeWrapper child)
    {
        if (_visibilityHandlers.ContainsKey(child)) return;
        PropertyChangedEventHandler handler = (_, args) =>
        {
            if (args.PropertyName == nameof(DataNodeWrapper.IsVisible))
                Post(() => Materialize(child));
        };
        _visibilityHandlers[child] = handler;
        child.PropertyChanged += handler;
    }

    private void UntrackVisibility(DataNodeWrapper child)
    {
        if (_visibilityHandlers.Remove(child, out PropertyChangedEventHandler? handler))
            child.PropertyChanged -= handler;
    }

    /// <summary>
    /// Inserts one already-created child wrapper at its schema position. This is an
    /// <c>Add</c> notification, not a reset, so no other field's view is rebuilt.
    /// </summary>
    private void Materialize(DataNodeWrapper child)
    {
        if (IsDisposed || !child.IsVisible || !_childByNode.ContainsKey(child.Node)) return;
        if (Children.Contains(child))
        {
            UntrackVisibility(child);
            return;
        }

        int index = 0;
        foreach ((StructFieldType field, IValueAccess value) in StructNodeImpl.GetFields())
        {
            DataNodeWrapper? wrapper = value is DataNode node ? _childByNode.GetValueOrDefault(node) : null;
            if (wrapper is null) continue;
            if (ReferenceEquals(wrapper, child)) break;
            if (Children.Contains(wrapper)) index++;
        }

        Children.Insert(Math.Min(index, Children.Count), child);
        // The row's IsVisible binding owns hiding from now on; never tear the element down.
        UntrackVisibility(child);
    }

    /// <inheritdoc />
    protected override void OnForcedReadOnlyChanged(bool value)
    {
        foreach (DataNodeWrapper child in _childByNode.Values)
            child.SetForcedReadOnly(value);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !IsDisposed)
        {
            foreach (DataNodeWrapper child in _visibilityHandlers.Keys.ToList())
                UntrackVisibility(child);
            foreach (DataNodeWrapper child in _childByNode.Values)
                child.Dispose();
            _childByNode.Clear();
            Children.ReplaceAll([]);
        }
        base.Dispose(disposing);
    }
}
