using SchemaNode.Node;

namespace SchemaNode.WPF.View.NodeWrapper;

/// <summary>
/// Last-resort wrapper for node shapes that have no dedicated view (for example a read-only
/// slice list). It never throws and never renders blank: the value is shown as read-only text
/// and the node type is exposed through the tooltip.
/// </summary>
public sealed class FallbackWrapper : DataNodeWrapper
{
    public FallbackWrapper(DataNode node, SchemaViewRegistry? registry, string? fieldName, SchemaViewRole role)
        : base(node, registry, fieldName, role)
    {
        TemplateKeyOverride = SchemaTemplateKeys.Fallback;
        System.Diagnostics.Debug.WriteLine(
            $"No dedicated view wrapper for '{node.Type.Name}' ({node.GetType().Name}); using read-only fallback.");
    }

    /// <summary>Node type name, used as the fallback label/tooltip.</summary>
    public string TypeName => NodeImpl.Type.Name;

    /// <summary>Best-effort text of the node value.</summary>
    public string DisplayText
    {
        get
        {
            try
            {
                if (NodeImpl.TryGetValue<string>(out string? text) && text is not null) return text;
                object? raw = NodeImpl.GetValue<object>();
                return raw?.ToString() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    /// <inheritdoc />
    protected override void OnValueChanged()
    {
        Post(() => RaisePropertyChangedNow(nameof(DisplayText)));
    }
}
