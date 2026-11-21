using System.Globalization;
using SchemaNode.Node;

namespace SchemaNode.Avalonia.View.NodeWrapper;

/// <summary>
/// Year editor for <c>system.year</c>. Mirrors yearView.vue: the value is a free integer, but the
/// control is a year stepper with a bounded range instead of the generic number input.
///
/// The wrapper keeps the year as a decimal so it binds directly to NumericUpDown; the node value
/// itself is still written as an integer.
/// </summary>
public sealed class YearWrapper : DataNodeWrapper
{
    /// <inheritdoc />
    public override string? DefaultTemplateKey => SchemaTemplateKeys.Year;

    public YearWrapper(DataNode node, SchemaViewRegistry? registry, string? fieldName, SchemaViewRole role)
        : base(node, registry, fieldName, role)
    {
        ScalarNodeImpl = node as ScalarNode
            ?? throw new ArgumentException($"Node '{node.Type.Name}' is not a scalar node.", nameof(node));
    }

    /// <summary>The typed node.</summary>
    public ScalarNode ScalarNodeImpl { get; }

    /// <summary>Lowest year offered by the stepper.</summary>
    public decimal Minimum => 1m;

    /// <summary>Highest year offered by the stepper.</summary>
    public decimal Maximum => 9999m;

    /// <summary>Year value, or null when the node is empty.</summary>
    public decimal? Year
    {
        get
        {
            object? raw = NodeImpl.GetValue<object>();
            if (raw is null) return null;
            try
            {
                return Convert.ToDecimal(raw, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return null;
            }
        }
        set
        {
            if (value is null)
            {
                Value = null;
                return;
            }

            Value = ((long)decimal.Round(value.Value)).ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <inheritdoc />
    protected override void OnValueChanged() => RaisePropertyChangedNow(nameof(Year));
}
