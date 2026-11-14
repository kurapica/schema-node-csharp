using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Reactive;
using Avalonia.VisualTree;
using SchemaNode.Avalonia.View.NodeWrapper;

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

    private TypeSelectorWrapper? _wrapper;

    public TypeSelectorView()
    {
        InitializeComponent();
        ApplyInputMinWidth();

        this.GetObservable(DataContextProperty).Subscribe(_ => AttachWrapper());
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

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        AttachWrapper();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        DetachWrapper();
        base.OnDetachedFromVisualTree(e);
    }

    private void ApplyInputMinWidth()
    {
        if (MainInput is null) return;

        MainInput.MinWidth = InputMinWidth;
        // The cascade field sizes itself inside its own star column, so the bound has to reach it.
        MainInput.CascaderFieldMinWidth = InputMinWidth;
    }

    private void AttachWrapper()
    {
        DetachWrapper();
        _wrapper = DataContext as TypeSelectorWrapper;
        if (_wrapper is not null)
        {
            _wrapper.PropertyChanged += OnWrapperPropertyChanged;
            _wrapper.Arguments.CollectionChanged += OnArgumentsChanged;
        }

        UpdateColumns();
    }

    private void DetachWrapper()
    {
        if (_wrapper is null) return;
        _wrapper.PropertyChanged -= OnWrapperPropertyChanged;
        _wrapper.Arguments.CollectionChanged -= OnArgumentsChanged;
        _wrapper = null;
    }

    private void OnWrapperPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TypeSelectorWrapper.HasGenerics)) UpdateColumns();
    }

    private void OnArgumentsChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateColumns();

    /// <summary>
    /// The path column stays one star; the generic part gets one star per argument, so the split
    /// follows namespace.vue's 1/(n+1) width share instead of a content-sized column.
    /// </summary>
    private void UpdateColumns()
    {
        if (Root.ColumnDefinitions.Count < 2) return;

        Root.ColumnDefinitions[1].Width = _wrapper is { HasGenerics: true } wrapper
                                          && wrapper.Arguments.Count > 0
            ? new GridLength(wrapper.Arguments.Count, GridUnitType.Star)
            : new GridLength(0);
    }
}
