using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Context;
using SchemaNode.Service;
using SchemaNode.WPF.Test.ViewModels;
using SchemaNode.WPF.View;
using SchemaNode.WPF.View.Debug;
using SchemaNode.WPF.View.Localization;
using System.Windows;

namespace SchemaNode.WPF.Test;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>

public partial class App : PrismApplication
{
    /// <summary>SchemaNode runtime root container (holds the ISchemaRuntime singleton)</summary>
    private ServiceProvider? _schemaServices;

    /// <summary>Schema scope owned by this single-window host (holds the scoped SchemaContext)</summary>
    private IServiceScope? _schemaScope;

    /// <summary>The shell ViewModel, kept so startup navigation failures can be reported in the shell.</summary>
    private MainWindowViewModel? _shellViewModel;

    /// <inheritdoc />
    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        // 1. Build the SchemaNode runtime: initialize only once when the host starts
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSchemaAssembly<App>(); 
        services.PrepareSchemaRuntime();
        _schemaServices = services.BuildServiceProvider();

        // 2. This sample owns one screen, so it owns one scope and one SchemaContext.
        //    FullMode must be enabled immediately after the context is created, before any
        //    type is loaded or any DataNode is created.
        _schemaScope = _schemaServices.CreateScope();
        SchemaContext context = _schemaScope.ServiceProvider.GetRequiredService<SchemaContext>();
        context.FullMode = true;

        // Locale must be applied here, before any type is loaded or any node is built: Core reads
        // SchemaContext.Locale when it produces validation text. The view layer keeps its own copy
        // for UI labels, so both sides have to switch together.
        context.Locale = LocaleManager.ZH_CN;
        LocaleManager.Instance.SetLocale(LocaleManager.ZH_CN);
        containerRegistry.RegisterInstance(context);

        // 3. All ViewModels are transient: each navigation gets a new view-level session
#if DEBUG
        // Enable the per-node inspector before the shell is created, so the first SchemaView
        // already builds its debug button. Release builds never reach this line.
        SchemaDebug.Enabled = true;
#endif
        containerRegistry.Register<MainWindowViewModel>();
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

        // Runtime initialization and node creation are genuinely asynchronous. They start here,
        // after the shell exists, and are never waited on synchronously, so the UI thread is never
        // blocked on schema loading.
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

        // The runtime is ready; only now may types be loaded and a DataNode be created.
        // The continuation captured the UI synchronization context, so the view model can
        // assign the node to its bound SchemaView on the UI thread.
        try
        {
            if (_shellViewModel is not null)
                await _shellViewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            ReportStartupFailure($"Sample node creation failed: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Sample node creation failed: {ex}");
        }
    }

    private void ReportStartupFailure(string message)
    {
        System.Diagnostics.Debug.WriteLine(message);
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        // Stop the view model first: it clears the bound node so the view releases its wrapper,
        // and only then is the DataNode disposed. The context and its scope come last.
        _shellViewModel?.Dispose();
        _schemaScope?.Dispose();
        _schemaServices?.Dispose();
        base.OnExit(e);
    }
}
