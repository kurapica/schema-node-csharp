using System.Net.Http;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Navigation.Regions;
using SchemaNode.Api.Client;
using SchemaNode.Data;
using SchemaNode.Schema.Provider;
using SchemaNode.Service;
using SchemaNode.WPF.Man.Services;
using SchemaNode.WPF.Man.ViewModels;
using SchemaNode.WPF.Man.Views;

namespace SchemaNode.WPF.Man;

/// <summary>
/// Prism application host.
///
/// Structural notes:
///   1. The SchemaNode runtime (<c>ISchemaRuntime</c> singleton) is built and initialized only once when the host starts;
///   2. A global <c>SchemaContext</c> singleton is no longer registered with the Prism container;
///      instead <see cref="SchemaContextFactory"/> is registered, and each view creates its own session when activated by navigation;
///   3. Local API providers are created along with the view context; the remote provider is stateless and can be shared globally.
/// </summary>
public partial class App : PrismApplication
{
    /// <summary>SchemaNode runtime root container (holds the ISchemaRuntime singleton)</summary>
    private ServiceProvider? _schemaServices;

    /// <summary>The shell ViewModel, kept so startup navigation failures can be reported in the shell.</summary>
    private MainWindowViewModel? _shellViewModel;

    /// <inheritdoc />
    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        // 1. Build the SchemaNode runtime: initialize only once when the host starts
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAppSchemaAssembly<App>(); // Scan Schema definitions in the current assembly
        // Local mode requires default application schema / node schema storage providers
        services.AddSchemaStorageProvider<DynamicAppEntryStorageProvider>();
        // App-data storage is required by the schema storage provider and by "system.wpf" setting
        services.AddAppDataProvider<InMemoryAppDataProvider>();
        services.PrepareSchemaRuntime();      // Register ISchemaRuntime (Singleton) / SchemaContext (Scoped)
        _schemaServices = services.BuildServiceProvider();

        // 2. The remote provider is stateless and can be shared; local providers are created per view by the factory
        ISchemaApiProvider? sharedRemote = CreateSharedRemote();
        containerRegistry.RegisterInstance(new SchemaContextFactory(_schemaServices, sharedRemote));

        // 2b. Host view registrations: struct fields / relations / enum values / app fields
        // render as tables through the registry instead of handwritten views.
        containerRegistry.RegisterInstance(ManSchemaViews.CreateRegistry());

        // 3. Language setting storage and switch coordination (singletons: one persisted setting per process)
        containerRegistry.RegisterSingleton<LanguageSettingsStore>();
        containerRegistry.RegisterSingleton<LanguageService>();

        // 4. All ViewModels are transient: each navigation gets a new view-level session
        containerRegistry.Register<MainWindowViewModel>();
        containerRegistry.Register<SchemaEditorViewModel>();
        containerRegistry.Register<TypeListViewModel>();
        containerRegistry.Register<TypeDocumentViewModel>();
        containerRegistry.Register<TypeDemoViewModel>();
        containerRegistry.Register<AppListViewModel>();
        containerRegistry.Register<AppFieldListViewModel>();
        containerRegistry.Register<AppFieldDefinitionViewModel>();
        containerRegistry.Register<AppWorkflowListViewModel>();
        containerRegistry.Register<AppDefinitionViewModel>();
        containerRegistry.Register<AppDemoViewModel>();
        containerRegistry.Register<AppFieldDemoViewModel>();
        containerRegistry.Register<WorkflowConfigViewModel>();
        containerRegistry.Register<FunctionDefinitionViewModel>();

        // 4. Register navigation views (the View's RegionMemberLifetime removes it on leave, avoiding reuse of an old context)
        containerRegistry.RegisterForNavigation<TypeListView>();
        containerRegistry.RegisterForNavigation<TypeDocumentView>();
        containerRegistry.RegisterForNavigation<TypeDemoView>();
        containerRegistry.RegisterForNavigation<AppListView>();
        containerRegistry.RegisterForNavigation<AppFieldListView>();
        containerRegistry.RegisterForNavigation<AppFieldDefinitionView>();
        containerRegistry.RegisterForNavigation<AppWorkflowListView>();
        containerRegistry.RegisterForNavigation<AppDefinitionView>();
        containerRegistry.RegisterForNavigation<AppDemoView>();
        containerRegistry.RegisterForNavigation<AppFieldDemoView>();
        containerRegistry.RegisterForNavigation<WorkflowConfigView>();
        containerRegistry.RegisterForNavigation<FunctionDefinitionView>();
    }

    /// <inheritdoc />
    protected override Window CreateShell()
    {
        MainWindow shell = Container.Resolve<MainWindow>();

        // Prism's convention resolver expects the shell under a ".Views" namespace, so the
        // auto-wire would leave the shell without a DataContext and every command/label dead.
        // Bind it explicitly instead of depending on the folder naming convention.
        MainWindowViewModel viewModel = Container.Resolve<MainWindowViewModel>();
        shell.DataContext = viewModel;
        _shellViewModel = viewModel;
        return shell;
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();

        // Runtime initialization and the first navigation are genuinely asynchronous. They start
        // here, after the shell and its region exist, and are never waited on synchronously, so
        // the UI thread is never blocked on schema loading.
        _ = StartAsync();
    }

    private async Task StartAsync()
    {
        try
        {
            if (_schemaServices is not null)
                await _schemaServices.InitSchemaRuntimeAsync();
        }
        catch (Exception ex)
        {
            // A runtime failure must not be silent: surface it in the shell and keep the app alive.
            ReportStartupFailure($"Schema runtime initialization failed: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Schema runtime initialization failed: {ex}");
            return;
        }

        await InitializeLanguageThenNavigateAsync();
    }

    private async Task InitializeLanguageThenNavigateAsync()
    {
        try
        {
            await Container.Resolve<LanguageService>().InitializeAsync();
        }
        catch (Exception ex)
        {
            // A storage failure must not break startup; the default locale stays in effect.
            System.Diagnostics.Debug.WriteLine($"Language initialization failed: {ex}");
        }

        // The continuation captured the UI synchronization context, so this runs on the UI thread.
        Container.Resolve<IRegionManager>().RequestNavigate(
            "MainContentRegion",
            nameof(TypeListView),
            result =>
            {
                if (!result.Success)
                    ReportStartupFailure(
                        $"Navigation to {nameof(TypeListView)} failed: {result.Exception?.Message ?? "unknown"}");
            });
    }

    private void ReportStartupFailure(string message)
    {
        if (_shellViewModel is not null)
            _shellViewModel.StatusMessage = message;
        System.Diagnostics.Debug.WriteLine(message);
    }

    /// <summary>
    /// Decides whether to use the remote provider based on the SCHEMA_API_BASE_URL environment variable.
    /// Returns null when unset, in which case <see cref="SchemaContextFactory"/> creates a local provider for each view.
    /// </summary>
    private static ISchemaApiProvider? CreateSharedRemote()
    {
        string? baseAddress = Environment.GetEnvironmentVariable("SCHEMA_API_BASE_URL");
        if (string.IsNullOrWhiteSpace(baseAddress)) return null;

        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        return new RemoteSchemaApiProvider(http, new SchemaApiProviderOptions { BaseAddress = baseAddress });
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        _schemaServices?.Dispose();
        base.OnExit(e);
    }
}
