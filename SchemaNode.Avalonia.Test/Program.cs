using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Avalonia.Test.ViewModels;
using SchemaNode.Avalonia.View.Localization;
using SchemaNode.Context;
using SchemaNode.Service;
#if DEBUG
using SchemaNode.Avalonia.View.Debug;
#endif

namespace SchemaNode.Avalonia.Test;

/// <summary>
/// Avalonia harness host. One screen means one scope and one SchemaContext; FullMode is turned on
/// immediately after the context is created, before any type is loaded or any node is built.
/// </summary>
public sealed class TestApp : Application
{
    private static ServiceProvider? _services;
    private static IServiceScope? _scope;
    private static MainWindowViewModel? _viewModel;

    public override void OnFrameworkInitializationCompleted()
    {
        // The WPF harness is light-only; follow it instead of the OS dark/light preference.
        RequestedThemeVariant = ThemeVariant.Light;
        Styles.Add(new FluentTheme());

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            MainWindow shell = new() { DataContext = _viewModel };
            desktop.MainWindow = shell;

            // Stop the view model first so the bound node is cleared and its wrapper is released,
            // then the DataNode (inside the view model), then the context's scope, then the root.
            shell.Closed += (_, _) => DisposeScope();
            _ = StartAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task StartAsync()
    {
        if (_services is null || _viewModel is null) return;

        try
        {
            await _services.InitSchemaRuntimeAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Schema runtime initialization failed: {ex}");
            return;
        }

        // The runtime is ready; only now may types be loaded and a DataNode be created.
        try
        {
            await _viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Sample node creation failed: {ex}");
        }
    }

    private static void DisposeScope()
    {
        _viewModel?.Dispose();
        _viewModel = null;
        _scope?.Dispose();
        _scope = null;
    }

    /// <summary>Builds the runtime container and the single screen session.</summary>
    public static ServiceProvider BuildServices()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSchemaAssembly<TestApp>();
        services.PrepareSchemaRuntime();
        _services = services.BuildServiceProvider();

        _scope = _services.CreateScope();
        SchemaContext context = _scope.ServiceProvider.GetRequiredService<SchemaContext>();
        context.FullMode = true;

        // Locale must be applied before any type is loaded: Core reads SchemaContext.Locale when it
        // produces validation text, and the view layer keeps its own copy for UI labels.
        context.Locale = LocaleManager.ZH_CN;
        LocaleManager.Instance.SetLocale(LocaleManager.ZH_CN);

#if DEBUG
        // Enable the per-node inspector before the shell is created, so the first SchemaView
        // already builds its debug button. Release builds never reach this line.
        SchemaDebug.Enabled = true;
#endif
        _viewModel = new MainWindowViewModel(context);
        return _services;
    }

    /// <summary>Builds the Avalonia application.</summary>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<TestApp>()
            .UsePlatformDetect()
            .LogToTrace();
}

/// <summary>Avalonia entry point.</summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        ServiceProvider services = TestApp.BuildServices();
        try
        {
            TestApp.BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            services.Dispose();
        }
    }
}
