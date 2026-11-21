using System.Globalization;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Utility;

namespace SchemaNode.Avalonia.View.NodeWrapper;

/// <summary>The date semantics a range struct carries.</summary>
public enum RangeDateKind
{
    /// <summary>Two years.</summary>
    Year,

    /// <summary>Two year-months.</summary>
    Month,

    /// <summary>Two dates.</summary>
    Date,

    /// <summary>Two dates with time.</summary>
    FullDate,
}

/// <summary>
/// Range editor for <c>system.rangeyear / rangemonth / rangedate / rangefulldate</c>.
///
/// The struct itself is not a container of independently rendered fields: the two bounds form one
/// control (start, separator, end). The start/end wrappers are still created so their Core
/// validation errors can be shown under the matching bound instead of lighting up the whole
/// range, which mirrors rangeDateView.vue.
/// </summary>
public sealed class RangeDateWrapper : DataNodeWrapper
{
    private readonly DataNodeWrapper? _startWrapper;
    private readonly DataNodeWrapper? _endWrapper;
    private string? _startFormatError;
    private string? _endFormatError;
    private string? _startMonthText;
    private string? _endMonthText;

    /// <inheritdoc />
    public override string? DefaultTemplateKey => SchemaTemplateKeys.RangeDate;

    public RangeDateWrapper(DataNode node, SchemaViewRegistry? registry, string? fieldName, SchemaViewRole role)
        : base(node, registry, fieldName, role)
    {
        StructNodeImpl = node as StructNode
            ?? throw new ArgumentException($"Node '{node.Type.Name}' is not a struct node.", nameof(node));

        string typeName = node.Type.Name;
        if (string.Equals(typeName, Constant.NS_SYSTEM_RANGE_YEAR, StringComparison.OrdinalIgnoreCase))
            Kind = RangeDateKind.Year;
        else if (string.Equals(typeName, Constant.NS_SYSTEM_RANGE_MONTH, StringComparison.OrdinalIgnoreCase))
            Kind = RangeDateKind.Month;
        else if (string.Equals(typeName, Constant.NS_SYSTEM_RANGE_FULL_DATE, StringComparison.OrdinalIgnoreCase))
            Kind = RangeDateKind.FullDate;
        else
            Kind = RangeDateKind.Date;

        _startWrapper = WrapBound("start", registry, role);
        _endWrapper = WrapBound("end", registry, role);
        _startWrapper?.PropertyChanged += OnBoundWrapperChanged;
        _endWrapper?.PropertyChanged += OnBoundWrapperChanged;
    }

    /// <summary>The typed node.</summary>
    public StructNode StructNodeImpl { get; }

    /// <summary>Range semantics.</summary>
    public RangeDateKind Kind { get; }

    /// <summary>Whether both bounds are years.</summary>
    public bool IsYear => Kind == RangeDateKind.Year;

    /// <summary>Whether both bounds are year-months.</summary>
    public bool IsMonth => Kind == RangeDateKind.Month;

    /// <summary>Whether both bounds are dates.</summary>
    public bool IsDate => Kind is RangeDateKind.Date or RangeDateKind.FullDate;

    /// <summary>Whether the range includes a time component.</summary>
    public bool IsFullDate => Kind == RangeDateKind.FullDate;

    /// <summary>Start bound as a year.</summary>
    public decimal? StartYear
    {
        get => _startWrapper is YearWrapper year ? year.Year : null;
        set
        {
            if (_startWrapper is YearWrapper year)
            {
                year.Year = value;
                RaisePropertyChangedNow(nameof(StartYear));
            }
        }
    }

    /// <summary>End bound as a year.</summary>
    public decimal? EndYear
    {
        get => _endWrapper is YearWrapper year ? year.Year : null;
        set
        {
            if (_endWrapper is YearWrapper year)
            {
                year.Year = value;
                RaisePropertyChangedNow(nameof(EndYear));
            }
        }
    }

    /// <summary>Start bound as a date.</summary>
    public DateTimeOffset? StartDate
    {
        get => _startWrapper is DateWrapper date ? date.Date : null;
        set
        {
            if (_startWrapper is DateWrapper date)
            {
                date.Date = value;
                RaisePropertyChangedNow(nameof(StartDate));
            }
        }
    }

    /// <summary>End bound as a date.</summary>
    public DateTimeOffset? EndDate
    {
        get => _endWrapper is DateWrapper date ? date.Date : null;
        set
        {
            if (_endWrapper is DateWrapper date)
            {
                date.Date = value;
                RaisePropertyChangedNow(nameof(EndDate));
            }
        }
    }

    /// <summary>Start year-month, formatted as <c>yyyy-MM</c>.</summary>
    public string? StartMonth
    {
        get => _startMonthText ?? ReadMonth(BoundNode("start"));
        set => WriteMonth("start", value, isStart: true);
    }

    /// <summary>End year-month, formatted as <c>yyyy-MM</c>.</summary>
    public string? EndMonth
    {
        get => _endMonthText ?? ReadMonth(BoundNode("end"));
        set => WriteMonth("end", value, isStart: false);
    }

    /// <summary>Error of the start bound, shown under the start control only.</summary>
    public string? StartError => _startFormatError ?? _startWrapper?.Error;

    /// <summary>Error of the end bound, shown under the end control only.</summary>
    public string? EndError => _endFormatError ?? _endWrapper?.Error;

    /// <summary>Whether the start bound has an error to show.</summary>
    public bool ShowStartError => !string.IsNullOrEmpty(StartError);

    /// <summary>Whether the end bound has an error to show.</summary>
    public bool ShowEndError => !string.IsNullOrEmpty(EndError);

    /// <inheritdoc />
    protected override void OnValueChanged()
    {
        // A year-month only stays pending while its text cannot be parsed. Any node-side update
        // means the value changed from elsewhere (relation, reset, source switch), so drop it.
        _startMonthText = null;
        _endMonthText = null;
        Post(RaiseBoundsChanged);
    }

    /// <inheritdoc />
    protected override void OnForcedReadOnlyChanged(bool value)
    {
        base.OnForcedReadOnlyChanged(value);
        _startWrapper?.SetForcedReadOnly(value);
        _endWrapper?.SetForcedReadOnly(value);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_startWrapper is not null) _startWrapper.PropertyChanged -= OnBoundWrapperChanged;
            if (_endWrapper is not null) _endWrapper.PropertyChanged -= OnBoundWrapperChanged;
            _startWrapper?.Dispose();
            _endWrapper?.Dispose();
            _startMonthText = null;
            _endMonthText = null;
        }
        base.Dispose(disposing);
    }

    private void OnBoundWrapperChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (IsDisposed) return;

        RaisePropertyChangedNow(nameof(StartYear));
        RaisePropertyChangedNow(nameof(EndYear));
        RaisePropertyChangedNow(nameof(StartDate));
        RaisePropertyChangedNow(nameof(EndDate));
        RaisePropertyChangedNow(nameof(StartError));
        RaisePropertyChangedNow(nameof(EndError));
        RaisePropertyChangedNow(nameof(ShowStartError));
        RaisePropertyChangedNow(nameof(ShowEndError));
    }

    private DataNodeWrapper? WrapBound(string field, SchemaViewRegistry? registry, SchemaViewRole role)
    {
        IValueAccess? bound = NodeImpl.GetAccessValue(field);
        return bound is DataNode dataNode ? NodeWrapperFactory.Create(dataNode, registry, field, role) : null;
    }

    private IValueAccess? BoundNode(string field) => NodeImpl.GetAccessValue(field);

    private void RaiseBoundsChanged()
    {
        if (IsDisposed) return;

        RaisePropertyChangedNow(nameof(StartYear));
        RaisePropertyChangedNow(nameof(EndYear));
        RaisePropertyChangedNow(nameof(StartDate));
        RaisePropertyChangedNow(nameof(EndDate));
        RaisePropertyChangedNow(nameof(StartMonth));
        RaisePropertyChangedNow(nameof(EndMonth));
        RaisePropertyChangedNow(nameof(StartError));
        RaisePropertyChangedNow(nameof(EndError));
        RaisePropertyChangedNow(nameof(ShowStartError));
        RaisePropertyChangedNow(nameof(ShowEndError));
    }

    private string? ReadMonth(IValueAccess? bound)
    {
        if (bound is null) return null;

        object? raw = bound.GetValue<object>();
        if (raw is null) return null;

        if (raw is DateOnly dateOnly)
            return dateOnly.ToString("yyyy-MM", CultureInfo.InvariantCulture);

        if (raw is DateTimeOffset dateTimeOffset)
            return dateTimeOffset.ToString("yyyy-MM", CultureInfo.InvariantCulture);

        if (raw is DateTime dateTime)
            return dateTime.ToString("yyyy-MM", CultureInfo.InvariantCulture);

        try
        {
            return DateTimeOffset.Parse(raw.ToString()!, CultureInfo.InvariantCulture)
                .ToString("yyyy-MM", CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private void WriteMonth(string field, string? text, bool isStart)
    {
        string? error = null;
        string? normalized = null;

        if (!string.IsNullOrWhiteSpace(text))
        {
            try
            {
                DateTime date = DateTime.ParseExact(
                    text!.Trim(), "yyyy-MM", CultureInfo.InvariantCulture,
                    DateTimeStyles.None);
                normalized = new DateTime(date.Year, date.Month, 1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            {
                error = Localization.LocaleManager.Instance.Get("avalonia.date.month.format");
            }
        }

        if (error is null)
        {
            // Valid text: commit it and clear the pending draft.
            IValueAccess? bound = BoundNode(field);
            bound?.TrySetValue(normalized);
            if (isStart) _startMonthText = null;
            else _endMonthText = null;
        }
        else if (isStart)
        {
            _startMonthText = text;
        }
        else
        {
            _endMonthText = text;
        }

        if (isStart) _startFormatError = error;
        else _endFormatError = error;

        RaiseBoundsChanged();
    }
}
