using SchemaNode.Node;
using SchemaNode.Property.Common;
using SchemaNode.Runtime;

namespace SchemaNode.WPF.View.NodeWrapper;

/// <summary>
/// Year value wrapper (counterpart of schema-node-vue-view's yearView).
///
/// A year is an integer, so the generic input would be enough for free editing. What the generic
/// input cannot do is honour a <c>WhiteList</c> / <c>BlackList</c> constraint as a closed choice,
/// which is exactly what the type view needs. The wrapper keeps that candidate list UI-friendly
/// and lets the view switch between a drop-down (constrained) and a plain year editor (free).
/// </summary>
public sealed class YearWrapper : DataNodeWrapper
{
    private bool _useWhiteList;

    public YearWrapper(DataNode node, SchemaViewRegistry? registry, string? fieldName, SchemaViewRole role,
                       bool ancestorVisibility = true)
        : base(node, registry, fieldName, role, ancestorVisibility)
    {
        // WhiteList / BlackList are schema properties; the constraint itself is validated by Core.
        // The view only mirrors the list so a year can be picked instead of typed.
        Track(node.SubscribeProperty<WhiteList, object[]>(OnWhiteListChanged, immediate: true));
        Track(node.SubscribeProperty<BlackList, object[]>(OnBlackListChanged, immediate: true));
    }

    /// <inheritdoc />
    public override string? DefaultTemplateKey => SchemaTemplateKeys.Year;

    /// <summary>Years offered by the white list (black-list entries removed), in declaration order.</summary>
    public BulkObservableCollection<long> WhiteListYears { get; } = new();

    /// <summary>Whether the year must be chosen from a white list.</summary>
    public bool UseWhiteList => _useWhiteList;

    /// <summary>
    /// Two-way year value. A null value clears the node; otherwise the raw year is written through
    /// Core so the usual Int constraints (low/up limit) still apply.
    /// </summary>
    public long? SelectedYear
    {
        get => NodeImpl.IsEmpty ? null : NodeImpl.GetValue<long>();
        set
        {
            if (IsDisposed) return;
            if (value is null)
            {
                NodeImpl.ClearValue();
                return;
            }
            NodeImpl.TrySetValue(value.Value);
        }
    }

    /// <inheritdoc />
    protected override void OnValueChanged()
    {
        Post(() => RaisePropertyChangedNow(nameof(SelectedYear)));
    }

    private void OnWhiteListChanged((IValueAccess node, Type propertyType, object[]? value, object[]? oldValue) args)
        => RefreshWhiteList();

    private void OnBlackListChanged((IValueAccess node, Type propertyType, object[]? value, object[]? oldValue) args)
        => RefreshWhiteList();

    private void RefreshWhiteList()
    {
        Post(() =>
        {
            if (IsDisposed) return;

            HashSet<long> blackList = ReadYears<BlackList>().ToHashSet();
            List<long> years = ReadYears<WhiteList>()
                .Where(year => !blackList.Contains(year))
                .ToList();

            WhiteListYears.ReplaceAll(years);

            bool useWhiteList = years.Count > 0;
            if (_useWhiteList != useWhiteList)
            {
                _useWhiteList = useWhiteList;
                RaisePropertyChangedNow(nameof(UseWhiteList));
            }
            RaisePropertyChangedNow(nameof(SelectedYear));
        });
    }

    private IEnumerable<long> ReadYears<TProperty>() where TProperty : SchemaNode.Property.Property<object[]>
    {
        object[]? values = NodeImpl.GetPropertyValue<TProperty, object[]>();
        if (values is null) yield break;
        foreach (object? value in values)
        {
            if (value is null) continue;
            if (long.TryParse(value.ToString(), out long year))
                yield return year;
        }
    }
}
