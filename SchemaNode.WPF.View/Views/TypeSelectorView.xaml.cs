using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using SchemaNode.WPF.View.NodeWrapper;

namespace SchemaNode.WPF.View.Views;

/// <summary>
/// Type path selector: the namespace / type cascade plus the generic argument editors, the WPF
/// counterpart of <c>schema-node-man/src/view/namespace.vue</c>.
/// </summary>
public partial class TypeSelectorView : UserControl
{
    private TypeSelectorWrapper? _wrapper;

    /// <summary>
    /// Minimum width applied to the path input (the <c>Main</c> editor). The template entry
    /// <c>MinWidth</c> only bounds this control; without a bound on the inner editor a host that
    /// sizes to content (an Auto row, a stack panel, a narrow dialog column) collapses the closed
    /// cascade field to its padding. Generic argument slots set it to zero so they keep their
    /// narrow column layout.
    /// </summary>
    public static readonly DependencyProperty InputMinWidthProperty = DependencyProperty.Register(
        nameof(InputMinWidth),
        typeof(double),
        typeof(TypeSelectorView),
        new PropertyMetadata(120d, OnInputMinWidthChanged));

    /// <summary>Minimum width of the inner path editor; zero means no lower bound.</summary>
    public double InputMinWidth
    {
        get => (double)GetValue(InputMinWidthProperty);
        set => SetValue(InputMinWidthProperty, value);
    }

    private static void OnInputMinWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        TypeSelectorView view = (TypeSelectorView)d;
        if (view.MainInput is not null)
            view.MainInput.MinWidth = (double)e.NewValue;
    }

    public TypeSelectorView()
    {
        InitializeComponent();
        MainInput.MinWidth = InputMinWidth;
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        Detach();
        _wrapper = DataContext as TypeSelectorWrapper;
        if (_wrapper is not null)
        {
            _wrapper.PropertyChanged += OnWrapperPropertyChanged;
            _wrapper.Arguments.CollectionChanged += OnArgumentsChanged;
        }
        UpdateColumns();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Detach();

    private void Detach()
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
    /// follows namespace.vue's 1/(n+1) width share instead of a fixed pixel size.
    /// </summary>
    private void UpdateColumns()
    {
        GenericColumn.Width = _wrapper is { HasGenerics: true, Arguments.Count: > 0 }
            ? new GridLength(_wrapper.Arguments.Count, GridUnitType.Star)
            : new GridLength(0);
    }
}
