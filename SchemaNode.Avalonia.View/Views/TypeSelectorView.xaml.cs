using Avalonia;
using Avalonia.Controls;

namespace SchemaNode.Avalonia.View.Views;

/// <summary>
/// Type path editor plus its generic argument editors.
/// <para>
/// The path editor carries its own minimum width. Hosts that measure to content (horizontal
/// stack panels, auto columns) otherwise collapse the closed editor to its content width, because
/// the editors' star columns fall back to content size during such a measure pass. Generic
/// argument slots opt out (<c>InputMinWidth="0"</c>) so they stay compressible.
/// </para>
/// </summary>
public partial class TypeSelectorView : UserControl
{
    /// <summary>Minimum width applied to the path input; 0 lets a generic slot shrink freely.</summary>
    public static readonly StyledProperty<double> InputMinWidthProperty =
        AvaloniaProperty.Register<TypeSelectorView, double>(nameof(InputMinWidth), 120d);

    public TypeSelectorView()
    {
        InitializeComponent();
        ApplyInputMinWidth();
    }

    /// <summary>Minimum width applied to the path input; 0 lets a generic slot shrink freely.</summary>
    public double InputMinWidth
    {
        get => GetValue(InputMinWidthProperty);
        set => SetValue(InputMinWidthProperty, value);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == InputMinWidthProperty)
            ApplyInputMinWidth();
    }

    private void ApplyInputMinWidth()
    {
        if (MainInput is null) return;

        MainInput.MinWidth = InputMinWidth;
        // The cascade field sizes itself inside its own star column, so the bound has to reach it.
        MainInput.CascaderFieldMinWidth = InputMinWidth;
    }
}
