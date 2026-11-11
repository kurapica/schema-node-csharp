using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using SchemaNode.Avalonia.View.NodeWrapper;
using SchemaNode.Node;

namespace SchemaNode.Avalonia.View;

/// <summary>
/// Recursive dispatch control. The registry chooses the view; the selected view renders the
/// wrapper's children through nested <see cref="SchemaView"/> instances.
///
/// Resolution is driven by the node, not by a pre-created wrapper. When the host hands a bare
/// <see cref="DataNode"/> the wrapper is materialized when the control enters the visual tree and
/// released when it leaves or is rebound. A host-owned wrapper is never disposed here.
///
/// Avalonia has no template selector, so <see cref="SchemaViewTemplates"/> materializes the view
/// for the registry key. Re-resolution only replaces the presenter content when the resolved key
/// or wrapper actually changed, which keeps an inherited skin/role/registry change from rebuilding
/// a subtree that resolves to the same view.
/// </summary>
public class SchemaView : Grid
{
    /// <summary>Node or wrapper to render. Null falls back to the inherited DataContext.</summary>
    public static readonly StyledProperty<object?> NodeProperty =
        AvaloniaProperty.Register<SchemaView, object?>(nameof(Node));

    /// <summary>Registry inherited by nested views.</summary>
    public static readonly StyledProperty<SchemaViewRegistry?> RegistryProperty =
        AvaloniaProperty.Register<SchemaView, SchemaViewRegistry?>(nameof(Registry), inherits: true);

    /// <summary>
    /// Render role. Inherited so a cell's nested fields resolve through the same role without
    /// rebinding every nested control; a child view can still set it explicitly.
    /// </summary>
    public static readonly StyledProperty<SchemaViewRole> RoleProperty =
        AvaloniaProperty.Register<SchemaView, SchemaViewRole>(nameof(Role), inherits: true);

    /// <summary>Skin inherited by nested views.</summary>
    public static readonly StyledProperty<string> SkinProperty =
        AvaloniaProperty.Register<SchemaView, string>(
            nameof(Skin),
            SchemaViewRegistry.DefaultSkin,
            inherits: true);

    private readonly ContentPresenter _presenter = new()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Top,
    };

    private DataNodeWrapper? _observed;
    private DataNodeWrapper? _owned;
    private DataNode? _pendingNode;
    private string? _templateKey;
    private bool _hasContent;
    private bool _inTree;

    static SchemaView()
    {
        NodeProperty.Changed.AddClassHandler<SchemaView>((view, _) => view.Attach(view.Node ?? view.DataContext));
        RegistryProperty.Changed.AddClassHandler<SchemaView>((view, _) => view.OnRegistryChanged());
        RoleProperty.Changed.AddClassHandler<SchemaView>((view, _) => view.OnRoleChanged());
        SkinProperty.Changed.AddClassHandler<SchemaView>((view, _) => view.ApplyContent(view._observed));
    }

    public SchemaView()
    {
        Children.Add(_presenter);
        this.GetObservable(DataContextProperty).Subscribe(_ =>
        {
            // An explicitly assigned Node wins over the inherited DataContext.
            if (Node is null) Attach(DataContext);
        });

#if DEBUG
        if (Debug.SchemaDebug.Enabled)
            AttachDebugInspector();
#endif
    }

    #region Properties

    /// <summary>Node or wrapper to render.</summary>
    public object? Node
    {
        get => GetValue(NodeProperty);
        set => SetValue(NodeProperty, value);
    }

    /// <summary>Registry used to resolve views; null falls back to the shared default.</summary>
    public SchemaViewRegistry? Registry
    {
        get => GetValue(RegistryProperty);
        set => SetValue(RegistryProperty, value);
    }

    /// <summary>Where this node is rendered.</summary>
    public SchemaViewRole Role
    {
        get => GetValue(RoleProperty);
        set => SetValue(RoleProperty, value);
    }

    /// <summary>Skin used by type-specific registrations.</summary>
    public string Skin
    {
        get => GetValue(SkinProperty);
        set => SetValue(SkinProperty, value);
    }

    /// <summary>The wrapper currently rendered.</summary>
    public DataNodeWrapper? Wrapper => _observed;

    #endregion

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _inTree = true;

        // Containers may be recycled after detach; a bare node recorded while detached is wrapped
        // here, at materialization time.
        Attach(Node ?? DataContext);
        if (_observed is not null) ApplyContent(_observed);

        BlankClickFocus.Attach(this);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _inTree = false;

        Detach();
        DisposeOwned();
        _pendingNode = null;
    }

    private void OnRegistryChanged()
    {
        if (!_inTree) return;

        // A bare node must be wrapped again through the new registry; a host-supplied wrapper
        // keeps its own registry and only needs the view re-resolved.
        if (_pendingNode is not null)
            Bind(EnsureOwned(_pendingNode));
        else if (_observed is not null)
            ApplyContent(_observed);
    }

    private void OnRoleChanged()
    {
        if (_pendingNode is not null && _inTree)
            Bind(EnsureOwned(_pendingNode));
        else
            ApplyContent(_observed);
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
            if (!_inTree)
            {
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
            // Loaded/DataContextChanged fire repeatedly for recycled containers. Re-applying there
            // would destroy and rebuild the whole visual subtree for nothing.
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

    private DataNodeWrapper EnsureOwned(DataNode node)
    {
        SchemaViewRegistry registry = Registry ?? SchemaViewRegistry.Default;
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
        _templateKey = null;
        _presenter.Content = null;
        _presenter.ClearValue(IsVisibleProperty);
    }

    private void DisposeOwned()
    {
        _owned?.Dispose();
        _owned = null;
    }

    private void ApplyContent(DataNodeWrapper? wrapper)
    {
        if (wrapper is null || wrapper.IsDisposed || !_inTree)
        {
            DismissContent();
            return;
        }

        SchemaViewRegistry registry = Registry ?? SchemaViewRegistry.Default;
        string key = wrapper.TemplateKeyOverride
                     ?? registry.Resolve(wrapper.Node, wrapper.Role, Skin).TemplateKey;

        bool sameContent = ReferenceEquals(_presenter.Content, wrapper);
        _hasContent = true;

        if (sameContent && string.Equals(_templateKey, key, StringComparison.Ordinal))
        {
            // Visibility is bound once; nothing else depends on property overrides.
            return;
        }

        _templateKey = key;
        _presenter.Content = SchemaViewTemplates.Create(key, wrapper);

        _presenter.Bind(IsVisibleProperty, new Binding(nameof(DataNodeWrapper.IsVisible))
        {
            Source = wrapper,
            Mode = BindingMode.OneWay,
        });
    }

    private void OnWrapperPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DataNodeWrapper.TemplateKeyOverride) &&
            sender is DataNodeWrapper wrapper)
            ApplyContent(wrapper);
    }

#if DEBUG
    private Debug.SchemaDebugPanel? _debugPanel;
    private ToggleButton? _debugToggle;
    private Popup? _debugPopup;

    /// <summary>
    /// Creates the inspector trigger at runtime. The whole method and every type it touches are
    /// compiled only for Debug, so a Release build contains neither the element nor the panel.
    /// </summary>
    private void AttachDebugInspector()
    {
        _debugToggle = new ToggleButton
        {
            Content = "\U0001F41E",
            FontSize = 11,
            Padding = new Thickness(2, 0, 2, 0),
            MinWidth = 0,
            MinHeight = 0,
            VerticalAlignment = VerticalAlignment.Top,
            Focusable = false,
        };
        ToolTip.SetTip(_debugToggle, "Node debug properties");

        _debugPopup = new Popup
        {
            Placement = PlacementMode.Right,
            PlacementTarget = _debugToggle,
            IsLightDismissEnabled = true,
        };

        _debugToggle.GetObservable(ToggleButton.IsCheckedProperty).Subscribe(OnDebugToggleChanged);

        // The Release layout is a single content column; the inspector adds its trigger column and
        // a star-sized content column only in Debug builds, so no layout is reserved elsewhere.
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        SetColumn(_presenter, 1);

        Children.Add(_debugToggle);
        Children.Add(_debugPopup);
    }

    private void OnDebugToggleChanged(bool? isChecked)
    {
        if (_debugToggle?.IsChecked != true)
        {
            if (_debugPopup is not null) _debugPopup.IsOpen = false;
            return;
        }

        DataNode? node = _observed?.Node ?? _pendingNode;
        if (node is null)
        {
            _debugPopup!.IsOpen = false;
            return;
        }

        _debugPanel ??= new Debug.SchemaDebugPanel();
        _debugPopup!.Child = _debugPanel;
        _debugPanel.Show(Debug.SchemaDebug.Describe(node));
        _debugPopup.IsOpen = true;
    }
#endif
}
