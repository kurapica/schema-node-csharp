using SchemaNode.Node;

namespace SchemaNode.WPF.View.NodeWrapper;

/// <summary>Wraps boolean scalars for a compact check box.</summary>
public sealed class BoolWrapper : ScalarWrapper
{
    /// <inheritdoc />
    public override string? DefaultTemplateKey => SchemaTemplateKeys.Boolean;

    public BoolWrapper(DataNode node, SchemaViewRegistry? registry, string? fieldName, SchemaViewRole role)
        : base(node, registry, fieldName, role)
    {
    }

    /// <summary>Checked state. Null means "not set" and is rendered as unchecked.</summary>
    public bool? IsChecked
    {
        get => NodeImpl.TryGetValue<bool>(out bool value) ? value : null;
        set => Value = value ?? false;
    }

    /// <inheritdoc />
    protected override void OnValueChanged()
    {
        RaisePropertyChanged(nameof(IsChecked));
    }
}
