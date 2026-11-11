using SchemaNode.Node;

namespace SchemaNode.WPF.View.NodeWrapper;

/// <summary>
/// Wraps <c>system.date</c>. Core stores <see cref="DateTimeOffset"/>; the editor binds a
/// <see cref="DateTime"/> because that is what WPF's DatePicker exposes.
/// </summary>
public sealed class DateWrapper : DataNodeWrapper
{
    /// <inheritdoc />
    public override string? DefaultTemplateKey => SchemaTemplateKeys.Date;

    public DateWrapper(DataNode node, SchemaViewRegistry? registry, string? fieldName, SchemaViewRole role,
                       bool ancestorVisibility = true)
        : base(node, registry, fieldName, role, ancestorVisibility)
    {
    }

    /// <summary>Date value in local time, or null when unset.</summary>
    public DateTime? Date
    {
        get
        {
            return NodeImpl.TryGetValue<DateTimeOffset>(out DateTimeOffset value)
                ? value.LocalDateTime
                : null;
        }
        set
        {
            if (value is null)
            {
                Value = null;
                return;
            }
            Value = new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Local));
        }
    }

    /// <inheritdoc />
    protected override void OnValueChanged()
    {
        RaisePropertyChanged(nameof(Date));
    }
}
