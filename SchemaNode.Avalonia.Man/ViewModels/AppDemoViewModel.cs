using System.Text.Json.Nodes;
using System.Windows.Input;
using SchemaNode.Avalonia.Man.Infrastructure;
using SchemaNode.Avalonia.Man.Navigation;
using SchemaNode.Avalonia.Man.Services;
using SchemaNode.Avalonia.View.Localization;

namespace SchemaNode.Avalonia.Man.ViewModels;

/// <summary>
/// App demo ViewModel (screen-scoped).
///
/// The app / target used for function calls are passed explicitly (mirroring the App/Target
/// fields of the server-side CallFunctionRequest);
/// the local provider writes them into this screen's SchemaContext, so they do not leak across screens.
/// </summary>
public class AppDemoViewModel : BindableBase, INavigationAware, IDestructible
{
    private readonly SchemaContextFactory _factory;
    private SchemaScreenSession? _session;
    private string _functionName = string.Empty;
    private string _argsJson = "[]";
    private string _appName = string.Empty;
    private string _target = string.Empty;
    private string? _result;
    private bool _isBusy;

    public AppDemoViewModel(SchemaContextFactory factory)
    {
        _factory = factory;
        CallCommand = new DelegateCommand(ExecuteCall, () => !string.IsNullOrWhiteSpace(FunctionName) && !IsBusy)
            .ObservesProperty(() => FunctionName)
            .ObservesProperty(() => IsBusy);
    }

    public string FunctionName
    {
        get => _functionName;
        set => SetProperty(ref _functionName, value);
    }

    public string ArgsJson
    {
        get => _argsJson;
        set => SetProperty(ref _argsJson, value);
    }

    /// <summary>Associated application full name (optional)</summary>
    public string AppName
    {
        get => _appName;
        set => SetProperty(ref _appName, value);
    }

    /// <summary>Associated target data full name (optional)</summary>
    public string Target
    {
        get => _target;
        set => SetProperty(ref _target, value);
    }

    public string? Result
    {
        get => _result;
        set => SetProperty(ref _result, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public ICommand CallCommand { get; }

    #region Navigation

    public bool IsNavigationTarget(NavigationContext navigationContext) => false;

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        Teardown();
        _session = _factory.CreateSession();
    }

    public void OnNavigatedFrom(NavigationContext navigationContext) => Teardown();

    public void Destroy() => Teardown();

    #endregion

    private async void ExecuteCall()
    {
        if (_session is null || string.IsNullOrWhiteSpace(_functionName)) return;

        IsBusy = true;
        Result = null;
        try
        {
            JsonArray? args = null;
            if (!string.IsNullOrWhiteSpace(_argsJson))
            {
                JsonNode? node = JsonNode.Parse(_argsJson);
                args = node as JsonArray ?? new JsonArray();
            }

            JsonNode? result = await _session.Api.CallFunctionAsync(
                _functionName,
                args,
                app: string.IsNullOrWhiteSpace(_appName) ? null : _appName.Trim(),
                target: string.IsNullOrWhiteSpace(_target) ? null : _target.Trim());

            Result = result?.ToJsonString() ?? "(null)";
        }
        catch (Exception ex)
        {
            Result = LocaleManager.Instance.Format("avalonia.appdemo.error", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Teardown()
    {
        _session?.Dispose();
        _session = null;
    }
}
