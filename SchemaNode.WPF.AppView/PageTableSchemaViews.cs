using System.Runtime.CompilerServices;
using SchemaNode.Node;
using SchemaNode.Property.App;
using SchemaNode.Runtime;
using SchemaNode.WPF.View;
using SchemaNode.WPF.View.NodeWrapper;
using SchemaNode.WPF.AppView.NodeWrapper;

namespace SchemaNode.WPF.AppView;

/// <summary>Resource key of the page table template.</summary>
public static class PageTableTemplateKeys
{
    /// <summary>Data template key a host must provide for <see cref="PageTableWrapper"/>.</summary>
    public const string PageTable = "SchemaPageTable";
}

/// <summary>
/// Binds an array node to a host page source and exposes it through the generic view registry.
///
/// A pageable field is not distinguishable by node type in C# Core (there is no PageNode), so the
/// host attaches the adapter to the node it created. The registry hook then resolves that node to
/// <see cref="PageTableTemplateKeys.PageTable"/> instead of the plain array/table view. This keeps
/// the dispatch node-driven and avoids a kind check in a host view.
/// </summary>
public static class PageTableSchemaViews
{
    private static readonly ConditionalWeakTable<DataNode, PageTableBinding> Bindings = new();

    /// <summary>
    /// Installs the node-specific resolver on a registry. Call once per host registry, after the
    /// normal table/array registrations are in place.
    /// </summary>
    public static void Enable(SchemaViewRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        Func<IValueAccess, SchemaViewRole, string, SchemaViewRegistration?>? previous = registry.NodeRegistrationResolver;
        registry.NodeRegistrationResolver = (node, role, skin) => Resolve(node, role, skin) ?? previous?.Invoke(node, role, skin);
    }

    /// <summary>
    /// Attaches a page adapter to an array node. The node must be shaped as a struct array so the
    /// underlying table machinery can derive columns and headers from the element type.
    /// </summary>
    public static void Bind(
        DataNode node,
        SchemaViewRegistry registry,
        IPageTableAdapter adapter,
        IReadOnlyList<FieldFilter>? filters = null,
        IReadOnlyList<string>? viewColumns = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(adapter);

        Bindings.AddOrUpdate(node, new PageTableBinding(registry, adapter, filters, viewColumns));
    }

    /// <summary>Whether a node was bound to a page adapter.</summary>
    public static bool IsBound(DataNode node) => Bindings.TryGetValue(node, out _);

    private static SchemaViewRegistration? Resolve(IValueAccess node, SchemaViewRole role, string skin)
    {
        // Page tables are top-level field surfaces. Nested cells and row editors keep the plain
        // table/array behaviour, mirroring vue where only the root PageNode selects pageTableView.
        if (role != SchemaViewRole.Field || node is not DataNode dataNode) return null;
        if (!Bindings.TryGetValue(dataNode, out PageTableBinding? binding)) return null;

        return new SchemaViewRegistration(
            PageTableTemplateKeys.PageTable,
            value => CreateWrapper(value, binding));
    }

    private static DataNodeWrapper CreateWrapper(IValueAccess node, PageTableBinding binding)
    {
        PageTableWrapper wrapper = new(
            (DataNode)node,
            binding.Registry,
            binding.Adapter,
            binding.Filters);

        if (binding.ViewColumns is { Count: > 0 })
            wrapper.ViewColumns = binding.ViewColumns;

        return wrapper;
    }

    private sealed record PageTableBinding(
        SchemaViewRegistry Registry,
        IPageTableAdapter Adapter,
        IReadOnlyList<FieldFilter>? Filters,
        IReadOnlyList<string>? ViewColumns);
}
