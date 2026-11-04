using SchemaNode.Enum;
using SchemaNode.Property.App;
using SchemaNode.WPF.View.NodeWrapper;

namespace SchemaNode.WPF.AppView;

/// <summary>
/// One filter bar entry, built from an <c>AppFieldType.Filters</c> item. A light filter owns a
/// single typed input; a function filter owns one input per function argument (the first argument
/// is the source element and is not user-editable, mirroring vue's pageTableView).
/// </summary>
public sealed class PageFilter
{
    internal PageFilter(FieldFilter config, string label, IReadOnlyList<DataNodeWrapper> inputs)
    {
        Config = config;
        Label = label;
        Inputs = inputs;
    }

    /// <summary>The field-level filter definition.</summary>
    public FieldFilter Config { get; }

    /// <summary>Localized caption shown in front of the inputs.</summary>
    public string Label { get; }

    /// <summary>Filter input wrappers, rendered through the normal schema field view.</summary>
    public IReadOnlyList<DataNodeWrapper> Inputs { get; }

    /// <summary>Whether this filter calls a function instead of applying a simple comparison.</summary>
    public bool IsFunction => Config.Mode == FieldFilterMode.Filter;

    /// <summary>
    /// Reads the current values. Returns false when the filter is incomplete; an incomplete
    /// filter is skipped instead of being sent as a partial constraint.
    /// </summary>
    public bool TryReadValues(out IReadOnlyList<object?> values)
    {
        List<object?> collected = new(Inputs.Count);
        foreach (DataNodeWrapper input in Inputs)
        {
            if (input.IsDisposed || input.Node.IsEmpty)
            {
                values = [];
                return false;
            }

            collected.Add(input.Node.GetValue<object>());
        }

        values = collected;
        return collected.Count > 0;
    }
}
