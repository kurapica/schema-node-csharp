using System.Windows.Input;
using SchemaNode.Avalonia.Man.Infrastructure;
using SchemaNode.Avalonia.Man.Navigation;
using SchemaNode.Avalonia.Man.Services;
using SchemaNode.Avalonia.Man.Views;
using SchemaNode.Avalonia.View.Localization;

namespace SchemaNode.Avalonia.Man.ViewModels;

/// <summary>
/// Type demo ViewModel (screen-scoped).
/// Each navigation activation creates its own <see cref="SchemaScreenSession"/>, attaches the editor,
/// then loads the default demo type; on leave the editor clears its bound node so the view disposes the wrapper, then releases the DataNode and context.
///
/// The demo is reached from a row action, so it also carries the namespace the row was listed in:
/// the back command returns to that level instead of the default root namespace.
/// </summary>
public class TypeDemoViewModel : BindableBase, INavigationAware, IDestructible
{
    /// <summary>Navigation parameter carrying the type to demo (defaults to system.string).</summary>
    public const string TypeParameter = "type";

    /// <summary>Navigation parameter carrying the namespace to return to (defaults to system).</summary>
    public const string NamespaceParameter = "namespace";

    private const string MainContentRegion = "MainContentRegion";
    private const string DefaultDemoType = "system.string";
    private const string DefaultNamespace = "system";

    private readonly SchemaContextFactory _factory;
    private readonly IRegionManager _regionManager;
    private SchemaScreenSession? _session;
    private string _namespace = DefaultNamespace;
    private string? _status;
    private bool _isNavigating;

    public TypeDemoViewModel(SchemaContextFactory factory, SchemaEditorViewModel editor, IRegionManager regionManager)
    {
        _factory = factory;
        _regionManager = regionManager;
        Editor = editor;
        BackCommand = new DelegateCommand(ExecuteBack);
    }

    public SchemaEditorViewModel Editor { get; }

    /// <summary>Loading / navigation failure message.</summary>
    public string? Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public ICommand BackCommand { get; }

    public bool IsNavigationTarget(NavigationContext navigationContext) => false;

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        Teardown();

        _namespace = navigationContext.Parameters.GetValue<string>(NamespaceParameter)
                     is { Length: > 0 } value
            ? value
            : DefaultNamespace;

        _session = _factory.CreateSession();
        Editor.Attach(_session);

        string typeName = navigationContext.Parameters.GetValue<string>(TypeParameter)
                          is { Length: > 0 } type
            ? type
            : DefaultDemoType;
        _ = Editor.ReloadAsync(typeName);
    }

    public void OnNavigatedFrom(NavigationContext navigationContext) => Teardown();

    public void Destroy() => Teardown();

    private async void ExecuteBack()
    {
        if (_isNavigating) return;
        _isNavigating = true;
        try
        {
            TaskCompletionSource<INavigationResult> completion = new();
            _regionManager.RequestNavigate(
                MainContentRegion,
                nameof(TypeListView),
                result => completion.TrySetResult(result),
                new NavigationParameters
                {
                    { NamespaceParameter, _namespace },
                });

            INavigationResult outcome = await completion.Task;
            if (outcome.Success) return;

            Status = LocaleManager.Instance.Format(
                "avalonia.type.status.navfailed",
                outcome.Exception?.Message ?? nameof(TypeListView));
        }
        finally
        {
            _isNavigating = false;
        }
    }

    private void Teardown()
    {
        Editor.Dispose();
        _session?.Dispose();
        _session = null;
    }
}
