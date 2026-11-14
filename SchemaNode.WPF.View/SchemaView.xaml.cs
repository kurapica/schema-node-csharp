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
    private readonly SchemaTemplateSelector _templateSelector = new SchemaTemplateSelector();
    private DataNodeWrapper? _observed;
    private DataNodeWrapper? _owned;
    private DataNode? _pendingNode;
    private bool _hasContent;

    public SchemaView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        // The property is inherited, so a host normally switches the inspector on once for a whole
        // subtree and a view created later receives it when it joins the tree. This covers an
        // explicit value that was already applied to this instance.
        if (GetDebug(this)) AttachDebugInspector();
    }

    private Debug.SchemaDebugPanel? _debugPanel;
    private System.Windows.Controls.Primitives.ToggleButton? _debugToggle;
    private System.Windows.Controls.Primitives.Popup? _debugPopup;

    /// <summary>
    /// Creates the inspector trigger. It is built on demand, so turning the switch off keeps both
    /// the element and the panel out of the tree instead of merely hiding them.
    /// </summary>
    private void AttachDebugInspector()
    {
        if (_debugToggle is not null) return;

        _debugToggle = new System.Windows.Controls.Primitives.ToggleButton
        {
            Content = "\U0001F41E",
            FontSize = 11,
            Padding = new Thickness(2, 0, 2, 0),
            MinWidth = 0,
            VerticalAlignment = VerticalAlignment.Top,
            Focusable = false,
            ToolTip = "Node debug properties"
        };
        _debugToggle.Checked += OnDebugToggleChecked;
        _debugToggle.Unchecked += OnDebugToggleUnchecked;

        _debugPopup = new System.Windows.Controls.Primitives.Popup
        {
            Placement = System.Windows.Controls.Primitives.PlacementMode.Right,
            PlacementTarget = _debugToggle,
            StaysOpen = false
        };

        Grid.SetColumn(_debugToggle, 1);
        Grid.SetColumn(_debugPopup, 1);

        // The normal layout is a single content column; the inspector appends its trigger after the
        // content, so the content keeps its leading edge and any host overlay aligned to this control
        // still lines up with the rendered editor. The trigger is capped to the content height so it
        // can never drive the row height.
        Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(Presenter, 0);
        _debugToggle.SetBinding(FrameworkElement.MaxHeightProperty, new Binding(nameof(FrameworkElement.ActualHeight))
        {
            Source = Presenter
        });

        Root.Children.Add(_debugToggle);
        Root.Children.Add(_debugPopup);
    }

    /// <summary>Removes the inspector trigger and its layout when the switch is turned off.</summary>
    private void DetachDebugInspector()
    {
        if (_debugToggle is null) return;

        _debugPopup!.IsOpen = false;
        _debugPopup.Child = null;
        _debugToggle.Checked -= OnDebugToggleChecked;
        _debugToggle.Unchecked -= OnDebugToggleUnchecked;
        Root.Children.Remove(_debugToggle);
        Root.Children.Remove(_debugPopup);

        _debugPanel = null;
        _debugToggle = null;
        _debugPopup = null;

        Root.ColumnDefinitions.Clear();
        Grid.SetColumn(Presenter, 0);
    }

    private void OnDebugToggleChecked(object sender, RoutedEventArgs e)
    {
        DataNode? node = _observed?.Node ?? _pendingNode;
        if (node is null)
        {
            _debugPopup!.IsOpen = false;
            return;
        }

        _debugPanel ??= new Debug.SchemaDebugPanel();
        _debugPopup!.Child = _debugPanel;
        _debugPanel.Show(Debug.SchemaDebug.Describe(node));
        _debugPopup!.IsOpen = true;
    }

    private void OnDebugToggleUnchecked(object sender, RoutedEventArgs e) => _debugPopup!.IsOpen = false;

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

    /// <summary>
    /// Skin name forwarded to the registry resolver. Inherited like <see cref="Registry"/>, so a
    /// host sets it once on the root view and every nested view resolves through the same skin.
    /// </summary>
    public static readonly DependencyProperty SkinProperty = DependencyProperty.Register(
        nameof(Skin),
        typeof(string),
        typeof(SchemaView),
        new FrameworkPropertyMetadata(
            SchemaViewRegistry.DefaultSkin,
            FrameworkPropertyMetadataOptions.Inherits,
            OnSkinChanged));

    /// <summary>Skin used by type-specific registrations.</summary>
    public string Skin
    {
        get => (string)GetValue(SkinProperty);
        set => SetValue(SkinProperty, value);
    }

    /// <summary>
    /// Whether this view renders the per-node inspector. Inherited like <see cref="Registry"/>, so a
    /// host sets it once on the window or on the root view and every nested view follows. The
    /// default is off, and nothing is created or laid out until it is turned on.
    /// </summary>
    public static readonly DependencyProperty DebugProperty = DependencyProperty.RegisterAttached(
        "Debug",
        typeof(bool),
        typeof(SchemaView),
        new FrameworkPropertyMetadata(
            false,
            FrameworkPropertyMetadataOptions.Inherits,
            OnDebugChanged));

    /// <summary>Turns the per-node inspector on or off for an element and everything below it.</summary>
    public static void SetDebug(DependencyObject element, bool value) => element.SetValue(DebugProperty, value);

    /// <summary>Whether the per-node inspector is on for the element.</summary>
    public static bool GetDebug(DependencyObject element) => (bool)element.GetValue(DebugProperty);

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

    private static void OnDebugChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not SchemaView view) return;

        if (e.NewValue is true)
            view.AttachDebugInspector();
        else
            view.DetachDebugInspector();
    }

    /// <summary>
    /// Re-runs template resolution without touching the applied wrapper. The presenter only gets a
    /// new template when the resolved one actually differs, so an inherited skin change that still
    /// resolves the same view never dismisses or rebuilds the presenter subtree.
    /// </summary>
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

        // The first selection can run before this control reaches a window, where host resources
        // are not reachable yet. Re-resolving now is a no-op unless the selected template changed.
        if (_observed is not null) ApplyContent(_observed);

        // Only now is the hosting window known. One subscription per window covers every schema
        // view in it, so clicking inert background commits the focused editor through LostFocus.
        BlankClickFocus.Attach(this);
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
        DismissContent();
    }

    private void DismissContent()
    {
        _hasContent = false;
        if (Presenter.Content is null && Presenter.ContentTemplate is null) return;

        BindingOperations.ClearBinding(Presenter, VisibilityProperty);
        Presenter.Content = null;
        Presenter.ContentTemplate = null;
    }

    private void DisposeOwned()
    {
        _owned?.Dispose();
        _owned = null;
    }

    /// <summary>
    /// Applies the wrapper and the template selected for the view's current role and skin.
    /// Both are compared with what the presenter already holds: a re-resolution only swaps the
    /// template when the result actually differs, so a change that resolves the same view never
    /// detaches the content or rebuilds the presenter subtree.
    /// </summary>
    private void ApplyContent(DataNodeWrapper? wrapper)
    {
        if (wrapper is null || wrapper.IsDisposed)
        {
            DismissContent();
            return;
        }

        DataTemplate? template = _templateSelector.SelectTemplate(wrapper, this);
        bool sameContent = ReferenceEquals(Presenter.Content, wrapper);
        bool sameTemplate = ReferenceEquals(Presenter.ContentTemplate, template);
        _hasContent = true;

        if (sameContent && sameTemplate) return;

        // Assigning a different template re-applies it in place; the content object itself and
        // every wrapper behind it stay attached.
        Presenter.ContentTemplate = template;
        Presenter.Content = wrapper;

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
