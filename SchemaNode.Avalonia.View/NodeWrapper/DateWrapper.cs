using SchemaNode.Node;

namespace SchemaNode.Avalonia.View.NodeWrapper;

/// <summary>
/// Wraps <c>system.date</c>. Core stores <see cref="DateTimeOffset"/>; the editor binds a
/// <see cref="DateTimeOffset"/> because that is what the Avalonia date picker exposes.
/// </summary>
public sealed class DateWrapper : DataNodeWrapper
{
    /// <inheritdoc />
    public override string? DefaultTemplateKey => SchemaTemplateKeys.Date;

    public DateWrapper(DataNode node, SchemaViewRegistry? registry, string? fieldName, SchemaViewRole role)
        : base(node, registry, fieldName, role)
    {
    }

    /// <summary>Date value in local time, or null when unset.</summary>
    public DateTimeOffset? Date
    {
        get => NodeImpl.TryGetValue<DateTimeOffset>(out DateTimeOffset value)
            ? value.ToLocalTime()
            : null;
        set
        {
            if (value is null)
            {
                Value = null;
                return;
            }
            Value = value;
        }
    }

    /// <inheritdoc />
    protected override void OnValueChanged()
    {
        RaisePropertyChanged(nameof(Date));
    }
}
