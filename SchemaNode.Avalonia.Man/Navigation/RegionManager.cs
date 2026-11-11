using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Avalonia.Man.ViewModels;
using SchemaNode.Avalonia.Man.Views;

namespace SchemaNode.Avalonia.Man.Navigation;

/// <summary>
/// Maps view names to their view model type and view factory. The WPF layer relied on Prism's
/// ViewModelLocator naming convention; an explicit table is cheaper in Avalonia and makes the
/// missing-route failure obvious.
/// </summary>
public sealed class ManViewFactory
{
    private static readonly Dictionary<string, (Type ViewModel, Func<Control> CreateView)> Entries = new(StringComparer.Ordinal)
    {
        [nameof(TypeListView)] = (typeof(TypeListViewModel), () => new TypeListView()),
        [nameof(TypeDocumentView)] = (typeof(TypeDocumentViewModel), () => new TypeDocumentView()),
        [nameof(TypeDemoView)] = (typeof(TypeDemoViewModel), () => new TypeDemoView()),
        [nameof(AppListView)] = (typeof(AppListViewModel), () => new AppListView()),
        [nameof(AppDefinitionView)] = (typeof(AppDefinitionViewModel), () => new AppDefinitionView()),
        [nameof(AppDemoView)] = (typeof(AppDemoViewModel), () => new AppDemoView()),
        [nameof(AppFieldListView)] = (typeof(AppFieldListViewModel), () => new AppFieldListView()),
        [nameof(AppFieldDefinitionView)] = (typeof(AppFieldDefinitionViewModel), () => new AppFieldDefinitionView()),
        [nameof(AppFieldDemoView)] = (typeof(AppFieldDemoViewModel), () => new AppFieldDemoView()),
        [nameof(AppWorkflowListView)] = (typeof(AppWorkflowListViewModel), () => new AppWorkflowListView()),
        [nameof(FunctionDefinitionView)] = (typeof(FunctionDefinitionViewModel), () => new FunctionDefinitionView()),
        [nameof(WorkflowConfigView)] = (typeof(WorkflowConfigViewModel), () => new WorkflowConfigView()),
    };

    /// <summary>View model type registered for a view name.</summary>
    public Type ViewModelType(string viewName)
    {
        if (!Entries.TryGetValue(viewName, out (Type ViewModel, Func<Control> CreateView) entry))
            throw new InvalidOperationException($"No view is registered for '{viewName}'.");

        return entry.ViewModel;
    }

    /// <summary>Creates a fresh view instance for a view name.</summary>
    public Control CreateView(string viewName)
    {
        if (!Entries.TryGetValue(viewName, out (Type ViewModel, Func<Control> CreateView) entry))
            throw new InvalidOperationException($"No view is registered for '{viewName}'.");

        return entry.CreateView();
    }

    /// <summary>Registers an additional view; used by later screens without touching the core table.</summary>
    public static void Register(string viewName, Type viewModelType, Func<Control> createView)
        => Entries[viewName] = (viewModelType, createView);
}

/// <summary>
/// Owns the single content region. Navigation creates the target view model through DI, attaches
/// the matching view, then tears the previous target down in the WPF order: deactivate, destroy
/// (wrapper → node → context → scope), then release the view's DataContext so nested SchemaViews
/// detach.
/// </summary>
public sealed class RegionManager : IRegionManager
{
    private readonly IServiceProvider _provider;
    private readonly ManViewFactory _viewFactory;
    private object? _currentViewModel;

    /// <summary>Creates the manager used by every navigation-aware view model.</summary>
    public RegionManager(IServiceProvider provider, ManViewFactory viewFactory)
    {
        _provider = provider;
        _viewFactory = viewFactory;
    }

    /// <summary>Currently displayed view; the shell binds its content host to this event.</summary>
    public Control? CurrentView { get; private set; }

    /// <summary>Raised after <see cref="CurrentView"/> was replaced.</summary>
    public event EventHandler? CurrentViewChanged;

    /// <inheritdoc />
    public void RequestNavigate(
        string regionName,
        string viewName,
        Action<INavigationResult>? navigationCallback = null,
        NavigationParameters? navigationParameters = null)
    {
        NavigationParameters parameters = navigationParameters ?? new NavigationParameters();
        NavigationContext context = new(parameters);

        Control? previousView = CurrentView;
        object? previousViewModel = _currentViewModel;

        try
        {
            object viewModel = _provider.GetRequiredService(_viewFactory.ViewModelType(viewName));
            Control view = _viewFactory.CreateView(viewName);
            view.DataContext = viewModel;

            if (viewModel is INavigationAware aware) aware.OnNavigatedTo(context);

            CurrentView = view;
            _currentViewModel = viewModel;
            CurrentViewChanged?.Invoke(this, EventArgs.Empty);

            if (previousViewModel is INavigationAware previousAware)
                previousAware.OnNavigatedFrom(new NavigationContext(new NavigationParameters()));
            if (previousViewModel is IDestructible destructible)
                destructible.Destroy();
            if (previousView is not null)
                previousView.DataContext = null;

            navigationCallback?.Invoke(new NavigationResult(null, context));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Navigation to {viewName} failed: {ex}");
            navigationCallback?.Invoke(new NavigationResult(ex, context));
        }
    }
}
