using System.ComponentModel;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.WPF.View.Localization;

namespace SchemaNode.WPF.View.NodeWrapper;

/// <summary>
/// Wraps boolean scalars. A required bool renders as a two-state check box; an optional bool or
/// <c>bool?</c> renders as a select so the unset state stays a real, explicitly selectable null.
/// </summary>
public sealed class BoolWrapper : DataNodeWrapper
{
    /// <inheritdoc />
    public override string? DefaultTemplateKey => SchemaTemplateKeys.Boolean;

    public BoolWrapper(DataNode node, SchemaViewRegistry? registry, string? fieldName, SchemaViewRole role)
        : base(node, registry, fieldName, role)
    {
        Options =
        [
            new BoolOption(0),
            new BoolOption(1),
            new BoolOption(2),
        ];
        RefreshOptionLabels();
    }

    /// <summary>
    /// Checked state. Null means "not set". A required bool never reports null, so its two-state
    /// check box cannot show an indeterminate state.
    /// </summary>
    public bool? IsChecked
    {
        get => NodeImpl.IsEmpty ? Require ? false : null : NodeImpl.GetValue<bool>();
        set
        {
            if (Require)
            {
                Value = value ?? false;
                return;
            }

            if (value is null)
            {
                // Core's scalar TrySetValue converts a null value to the type default (false) for
                // value types, so ClearValue is the API that actually resets the node to unset.
                NodeImpl.ClearValue();
                SetInputError(null);
                return;
            }

            Value = value;
        }
    }

    /// <summary>Options of the optional select: unset, yes, no. Their labels follow the locale.</summary>
    public IReadOnlyList<BoolOption> Options { get; }

    /// <summary>Selected option of the optional select: 0 = unset, 1 = true, 2 = false.</summary>
    public int SelectedIndex
    {
        get => IsChecked switch
        {
            null => 0,
            true => 1,
            _ => 2,
        };
        set
        {
            // A control reset can report -1; that must not clear the node value.
            if (value < 0 || value > 2) return;
            IsChecked = value switch
            {
                1 => true,
                2 => false,
                _ => null,
            };
        }
    }

    /// <inheritdoc />
    protected override void OnValueChanged()
    {
        RaisePropertyChanged(nameof(IsChecked));
        RaisePropertyChanged(nameof(SelectedIndex));
    }

    /// <inheritdoc />
    protected override void OnRequireChanged((IValueAccess node, Type propertyType, bool value, bool oldValue) args)
    {
        base.OnRequireChanged(args);

        // A relation may change Require at runtime, which switches the editor branch and may also
        // change whether IsChecked may report null.
        RaisePropertyChanged(nameof(IsRequired));
        RaisePropertyChanged(nameof(IsOptional));
        RaisePropertyChanged(nameof(IsChecked));
    }

    /// <inheritdoc />
    protected override void OnLanguageChanged()
    {
        Post(RefreshOptionLabels);
    }

    private void RefreshOptionLabels()
    {
        Options[0].SetLabel(LocaleManager.Instance.Get("wpf.bool.unset"));
        Options[1].SetLabel(LocaleManager.Instance.Get("wpf.bool.yes"));
        Options[2].SetLabel(LocaleManager.Instance.Get("wpf.bool.no"));
    }

    /// <summary>One selectable option of the optional bool editor.</summary>
    public sealed class BoolOption : INotifyPropertyChanged
    {
        internal BoolOption(int index)
        {
            Index = index;
        }

        /// <summary>0 = unset/null, 1 = true, 2 = false.</summary>
        public int Index { get; }

        /// <summary>Localized label shown by the select.</summary>
        public string Label { get; private set; } = string.Empty;

        /// <inheritdoc />
        public event PropertyChangedEventHandler? PropertyChanged;

        internal void SetLabel(string label)
        {
            if (string.Equals(Label, label, StringComparison.Ordinal)) return;
            Label = label;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
        }
    }
}
