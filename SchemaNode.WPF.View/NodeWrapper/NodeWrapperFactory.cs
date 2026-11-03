using SchemaNode.Node;
using SchemaNode.Property.Core;
using SchemaNode.Property.String;
using SchemaNode.Runtime;

namespace SchemaNode.WPF.View.NodeWrapper;

/// <summary>
/// Creates the wrapper matching a node's data shape. This is the single construction entry point
/// used by parent wrappers (struct fields, array elements, table rows) and by hosts that render a
/// root node (Man passes <see cref="IValueAccess"/> handles).
/// </summary>
public static class NodeWrapperFactory
{
    /// <summary>Creates the appropriate wrapper for <paramref name="node"/>.</summary>
    public static DataNodeWrapper Create(
        IValueAccess node,
        SchemaViewRegistry? registry = null,
        string? fieldName = null,
        SchemaViewRole role = SchemaViewRole.Field)
    {
        DataNode dataNode = node as DataNode
            ?? throw new ArgumentException("Only DataNode instances can be wrapped for rendering.", nameof(node));
        SchemaViewRegistry effectiveRegistry = registry ?? SchemaViewRegistry.Default;

        // A type/kind registration may supply its own wrapper. This path makes the override apply
        // to nested nodes as well, not just to a root handed to SchemaView.
        Func<IValueAccess, DataNodeWrapper>? custom = effectiveRegistry.GetWrapperFactory(
            dataNode, role, SchemaViewRegistry.DefaultSkin);
        if (custom is not null)
            return custom(dataNode);

        if (dataNode is EnumArrayNode)
            return new InputWrapper(dataNode, effectiveRegistry, fieldName, role);

        if (dataNode is ArrayNode array)
            return effectiveRegistry.ShouldUseTable(array)
                ? new TableWrapper(dataNode, effectiveRegistry, fieldName, role)
                : new ArrayWrapper(dataNode, effectiveRegistry, fieldName, role);

        // Specialized list nodes (function call argument lists and similar) expose their elements
        // but do not derive from ArrayNode; they render read-only through the array view.
        if (dataNode is IEnumerable<IValueAccess>)
            return new ArrayWrapper(dataNode, effectiveRegistry, fieldName, role);

        if (dataNode is StructNode)
            return new StructWrapper(dataNode, effectiveRegistry, fieldName, role);

        if (dataNode is DateNode)
            return new DateWrapper(dataNode, effectiveRegistry, fieldName, role);

        if (dataNode is BoolNode)
            return new BoolWrapper(dataNode, effectiveRegistry, fieldName, role);

        if (dataNode is ScalarNode scalar)
        {
            // Core resolves entry sources asynchronously (debounced refresh), so HasEntrySource is
            // still false right after the node is created. The entry-source contract declared by
            // the type or by an ancestor provider is available synchronously; checking it prevents
            // a cascade selector (namespace / type selectors) from degrading into a plain TextBox.
            return scalar.HasEntrySource || ExpectsEntrySource(scalar)
                ? new InputWrapper(dataNode, effectiveRegistry, fieldName, role)
                : new ScalarWrapper(dataNode, effectiveRegistry, fieldName, role);
        }

        // Unknown shapes must not abort the parent tree (which would leave the whole pane blank):
        // degrade to a read-only text view and keep the rest of the form usable.
        return new FallbackWrapper(dataNode, effectiveRegistry, fieldName, role);
    }

    /// <summary>
    /// Whether the node is declared to have an entry source, even though Core has not resolved it
    /// yet. Mirrors the lookups <c>ScalarNode.RefreshEntrySourceAsync</c> performs: a direct
    /// <c>EntrySource</c>, a provider on an ancestor composite, or the consumer flags.
    /// </summary>
    private static bool ExpectsEntrySource(DataNode node)
        => node.GetProperty<EntrySource>()?.HasValue == true
           || node.GetProperty<EntrySourceProvider>()?.HasValue == true
           || node.GetProperty<EntrySourceConsumer>()?.Value == true
           || node.GetProperty<AccessEntryConsumer>()?.HasValue == true;
}
