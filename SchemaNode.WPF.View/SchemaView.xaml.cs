using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SchemaNode.Node;
using SchemaNode.WPF.View.NodeWrapper;

namespace SchemaNode.WPF.View;

/// <summary>
/// Recursive dispatch control. The template selector chooses the component view from the registry;
/// the selected view renders the wrapper's children through nested <see cref="SchemaView"/> instances.
///
/// Resolution is driven by the node, not by a pre-created wrapper. When the host hands a bare
/// <see cref="DataNode"/>, this control materializes the wrapper when it is loaded and releases it
/// when it is unloaded or rebound. A host that already owns a wrapper can still pass it in; that
/// wrapper is never disposed here.
/// </summary>
public partial class SchemaView : UserControl
{
    private DataNodeWrapper? _observed;
    private DataNodeWrapper? _owned;
    private DataNode? _pendingNode;
    private bool _hasContent;

    public SchemaView()
    {
        InitializeComponent();

        Presenter.ContentTemplateSelector = new SchemaTemplateSelector();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

#if DEBUG
        if (Debug.SchemaDebug.Enabled)
        {
            DebugToggle.Visibility = Visibility.Visible;
            DebugToggle.Checked += OnDebugToggleChecked;
            DebugToggle.Unchecked += OnDebugToggleUnchecked;
        }
#endif
    }

#if DEBUG
    private Debug.SchemaDebugPanel? _debugPanel;

    private void OnDebugToggleChecked(object sender, RoutedEventArgs e)
    {
        DataNode? node = _observed?.Node ?? _pendingNode;
        if (node is null)
        {
            DebugPopup.IsOpen = false;
            return;
        }

        _debugPanel ??= new Debug.SchemaDebugPanel();
        DebugPopup.Child = _debugPanel;
        _debugPanel.Show(Debug.SchemaDebug.Describe(node));
        DebugPopup.IsOpen = true;
    }

    private void OnDebugToggleUnchecked(object sender, RoutedEventArgs e) => DebugPopup.IsOpen = false;
#endif

    /// <summary>
    /// Node or wrapper to render. Accepts either a <see cref="DataNodeWrapper"/> (owned by the host)
    /// or a bare <see cref="DataNode"/> (wrapped and owned by this control). When null, the inherited
    /// DataContext is used.
    /// </summary>
    public static readonly DependencyProperty NodeProperty = DependencyProperty.Register(
        nameof(Node),
        typeof(object),
        typeof(SchemaView),
        new PropertyMetadata(null, OnNodeChanged));

    /// <summary>The node or wrapper to render.</summary>
    public object? Node
    {
        get => GetValue(NodeProperty);
        set => SetValue(NodeProperty, value);
    }

    /// <summary>
    /// Registry used when this control has to materialize a wrapper for a bare node. Inherited by
    /// nested SchemaView instances, so a host only sets it once on the root view.
    /// </summary>
    public static readonly DependencyProperty RegistryProperty = DependencyProperty.Register(
        nameof(Registry),
        typeof(SchemaViewRegistry),
        typeof(SchemaView),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.Inherits,
            OnRegistryChanged));

    /// <summary>Registry used to resolve templates and custom wrapper factories.</summary>
    public SchemaViewRegistry? Registry
    {
        get => (SchemaViewRegistry?)GetValue(RegistryProperty);
        set => SetValue(RegistryProperty, value);
    }

    /// <summary>Rendering role forwarded to the registry resolver.</summary>
    public static readonly DependencyProperty RoleProperty = DependencyProperty.Register(
        nameof(Role),
        typeof(SchemaViewRole),
        typeof(SchemaView),
        new PropertyMetadata(SchemaViewRole.Field, OnRoleChanged));

    /// <summary>Where this node is rendered.</summary>
    public SchemaViewRole Role
    {
        get => (SchemaViewRole)GetValue(RoleProperty);
        set => SetValue(RoleProperty, value);
    }

    /// <summary>Skin name forwarded to the registry resolver.</summary>
    public static readonly DependencyProperty SkinProperty = DependencyProperty.Register(
        nameof(Skin),
        typeof(string),
        typeof(SchemaView),
        new PropertyMetadata(SchemaViewRegistry.DefaultSkin, OnSkinChanged));

    /// <summary>Skin used by type-specific registrations.</summary>
    public string Skin
    {
        get => (string)GetValue(SkinProperty);
        set => SetValue(SkinProperty, value);
    }

    private SchemaViewRegistry EffectiveRegistry => Registry ?? SchemaViewRegistry.Default;

    private static void OnNodeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SchemaView view)
            view.Attach(view.Node ?? view.DataContext);
    }

    private static void OnRegistryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not SchemaView view || !view.IsLoaded) return;

        // A bare node has to be wrapped again through the new registry; a host-supplied wrapper
        // keeps its own registry and only needs the selector to run again.
        if (view._pendingNode is not null)
            view.Bind(view.EnsureOwned(view._pendingNode));
        else if (view._observed is not null)
            view.ApplyContent(view._observed);
    }

    private static void OnRoleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not SchemaView view) return;

        // The wrapper created for a bare node carries the role; a host-supplied wrapper keeps its
        // own. Only the selector needs a refresh in that case.
        if (view._pendingNode is not null && view.IsLoaded)
            view.Bind(view.EnsureOwned(view._pendingNode));
        else
            view.RefreshContent();
    }

    private static void OnSkinChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SchemaView view)
            view.RefreshContent();
    }

    /// <summary>Re-runs the template selector without touching the wrapper.</summary>
    private void RefreshContent() => ApplyContent(_observed);

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // An explicitly assigned Node wins over the inherited DataContext.
        if (Node is null)
            Attach(e.NewValue);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Containers may be recycled after Unloaded detached the wrapper. A bare node recorded while
        // unloaded is wrapped here, at materialization time.
        Attach(Node ?? DataContext);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Detach();
        DisposeOwned();
        _pendingNode = null;
    }

    private void Attach(object? context)
    {
        if (context is DataNodeWrapper wrapper)
        {
            _pendingNode = null;
            Bind(wrapper);
            return;
        }

        if (context is DataNode node)
        {
            _pendingNode = node;
            if (!IsLoaded)
            {
                // Materialization is the moment the wrapper is created; until then only the node is
                // remembered, so a view model can pass a whole tree without eager wrapping.
                Detach();
                DisposeOwned();
                return;
            }

            Bind(EnsureOwned(node));
            return;
        }

        _pendingNode = null;
        Detach();
        DisposeOwned();
    }

    private void Bind(DataNodeWrapper? next)
    {
        if (ReferenceEquals(_observed, next))
        {
            // Loaded/DataContextChanged fire repeatedly for recycled containers. Re-applying the
            // content there would destroy and rebuild the whole visual subtree for nothing.
            if (_hasContent || next is null) return;
            ApplyContent(next);
            return;
        }

        Detach();
        if (next is null) return;

        _observed = next;
        _observed.PropertyChanged += OnWrapperPropertyChanged;
        ApplyContent(next);
    }

    /// <summary>
    /// Wraps a bare node through the registry, reusing the current wrapper when it still matches.
    /// </summary>
    private DataNodeWrapper EnsureOwned(DataNode node)
    {
        SchemaViewRegistry registry = EffectiveRegistry;
        if (_owned is { IsDisposed: false } &&
            ReferenceEquals(_owned.Node, node) &&
            _owned.Role == Role &&
            _owned.Registry == registry)
            return _owned;

        DisposeOwned();

        SchemaViewRegistration registration = registry.Resolve(node, Role, Skin);
        _owned = registration.WrapperFactory?.Invoke(node)
                 ?? NodeWrapperFactory.Create(node, registry, null, Role);
        return _owned;
    }

    private void Detach()
    {
        if (_observed is not null)
            _observed.PropertyChanged -= OnWrapperPropertyChanged;
        _observed = null;
        _hasContent = false;

        BindingOperations.ClearBinding(Presenter, VisibilityProperty);
        Presenter.Content = null;
    }

    private void DisposeOwned()
    {
        _owned?.Dispose();
        _owned = null;
    }

    /// <summary>Forces the ContentPresenter to run the template selector again.</summary>
    private void ApplyContent(DataNodeWrapper? wrapper)
    {
        Presenter.Content = null;
        _hasContent = false;
        if (wrapper is null || wrapper.IsDisposed) return;

        Presenter.Content = wrapper;
        _hasContent = true;

        Binding visibility = new(nameof(DataNodeWrapper.IsVisible))
        {
            Source = wrapper,
            Mode = BindingMode.OneWay,
            Converter = Converters.BoolToVisibility
        };
        BindingOperations.SetBinding(Presenter, VisibilityProperty, visibility);
    }

    private void OnWrapperPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Visibility is covered by the binding above; the selector only has to re-run when a
        // host replaces the template override at runtime.
        if (e.PropertyName == nameof(DataNodeWrapper.TemplateKeyOverride) &&
            sender is DataNodeWrapper wrapper)
            ApplyContent(wrapper);
    }
}
