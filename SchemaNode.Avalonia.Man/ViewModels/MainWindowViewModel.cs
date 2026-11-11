using Avalonia.Controls;
using SchemaNode.Avalonia.Man.Infrastructure;
using SchemaNode.Avalonia.Man.Navigation;
using SchemaNode.Avalonia.Man.Services;
using SchemaNode.Avalonia.View.Localization;

namespace SchemaNode.Avalonia.Man.ViewModels;

/// <summary>
/// Main window ViewModel.
/// IRegionManager is injected and used to trigger Region navigation through DelegateCommand.
/// Prism Region navigation: RequestNavigate("RegionName", "ViewName") makes the specified View
/// appear inside the corresponding Region, achieving SPA-style page switching.
///
/// Language switch: the command updates the shared <see cref="LocaleManager"/> (XAML text bindings
/// refresh through its Version property), pushes the locale to all live SchemaContexts and persists it.
/// </summary>
public class MainWindowViewModel : BindableBase
{
    private readonly IRegionManager _regionManager;
    private readonly LanguageService _languageService;

    /// <summary>
    /// Constructor-injected IRegionManager (resolved automatically by Prism from the container).
    /// </summary>
    public MainWindowViewModel(IRegionManager regionManager, LanguageService languageService)
    {
        _regionManager = regionManager;
        _languageService = languageService;

        // DelegateCommand is the ICommand implementation provided by Prism;
        // here the navigation parameter (the View name) is forwarded to RequestNavigate
        NavigateCommand = new DelegateCommand<string>(Navigate);
        SwitchLanguageCommand = new DelegateCommand<string>(SwitchLanguage);

        // Keep the language toggle in sync when the locale is changed from anywhere
        LocaleManager.Instance.LanguageChanged += OnLanguageChanged;

        // The shell's content host follows the single region: the manager publishes the new view
        // after each navigation, and the window binds Content to this property.
        if (regionManager is RegionManager concrete)
        {
            CurrentContent = concrete.CurrentView;
            concrete.CurrentViewChanged += (_, _) =>
            {
                CurrentContent = concrete.CurrentView;
                RaisePropertyChanged(nameof(CurrentContent));
            };
        }
    }

    /// <summary>Currently displayed view hosted by the shell's content area.</summary>
    public Control? CurrentContent
    {
        get => _currentContent;
        private set => SetProperty(ref _currentContent, value);
    }

    private Control? _currentContent;

    /// <summary>Navigation command; CommandParameter is the target View name</summary>
    public DelegateCommand<string> NavigateCommand { get; }

    /// <summary>Language switch command; CommandParameter is "enUS" or "zhCN"</summary>
    public DelegateCommand<string> SwitchLanguageCommand { get; }

    /// <summary>Last navigation failure, shown in the shell so a broken route is never silent.</summary>
    public string? StatusMessage
    {
        get => _statusMessage;
        internal set => SetProperty(ref _statusMessage, value);
    }

    private string? _statusMessage;

    /// <summary>Two-way check state for the "EN" toggle</summary>
    public bool IsEnglish
    {
        get => LocaleManager.Instance.CurrentLocale.Equals(LocaleManager.EN_US, StringComparison.OrdinalIgnoreCase);
        set
        {
            if (value) SwitchLanguage(LocaleManager.EN_US);
        }
    }

    /// <summary>Two-way check state for the Chinese toggle</summary>
    public bool IsChinese
    {
        get => LocaleManager.Instance.CurrentLocale.Equals(LocaleManager.ZH_CN, StringComparison.OrdinalIgnoreCase);
        set
        {
            if (value) SwitchLanguage(LocaleManager.ZH_CN);
        }
    }

    /// <summary>
    /// Navigation command handler. It awaits the Prism navigation result so a failure inside
    /// <c>OnNavigatedTo</c> is reported instead of silently leaving the region unchanged.
    /// </summary>
    private async void Navigate(string? viewName)
    {
        if (string.IsNullOrWhiteSpace(viewName)) return;

        try
        {
            TaskCompletionSource<INavigationResult> completion = new();
            _regionManager.RequestNavigate(
                "MainContentRegion", viewName, result => completion.TrySetResult(result));

            INavigationResult outcome = await completion.Task;
            if (outcome.Success)
            {
                StatusMessage = null;
            }
            else
            {
                StatusMessage = $"Navigation to {viewName} failed: {outcome.Exception?.Message}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Navigation to {viewName} failed: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Navigation to {viewName} failed: {ex}");
        }
    }

    private async void SwitchLanguage(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale)) return;

        try
        {
            await _languageService.SwitchAsync(locale);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Language switch failed: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Language switch failed: {ex}");
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        RaisePropertyChanged(nameof(IsEnglish));
        RaisePropertyChanged(nameof(IsChinese));
    }
}
