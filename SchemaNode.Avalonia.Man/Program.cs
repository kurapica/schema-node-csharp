using System.Net.Http;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using SchemaNode.AI;
using SchemaNode.AI.Chat;
using SchemaNode.Api.Client;
using SchemaNode.Avalonia.Man.Infrastructure;
using SchemaNode.Avalonia.Man.Navigation;
using SchemaNode.Avalonia.Man.Services;
using SchemaNode.Avalonia.Man.ViewModels;
using SchemaNode.Avalonia.Man.Views;
using SchemaNode.Avalonia.View;
using SchemaNode.Data;
using SchemaNode.Schema.Provider;
using SchemaNode.Service;
using SchemaNode.Utility;

namespace SchemaNode.Avalonia.Man;

/// <summary>
/// Avalonia host for the Man surfaces.
///
/// Structural notes (mirrors the WPF host):
///   1. The SchemaNode runtime is built and initialized once at startup;
///   2. There is no global SchemaContext; <see cref="SchemaContextFactory"/> creates one session
///      per screen and every screen sets FullMode before use;
///   3. A single content region (<see cref="RegionManager"/>) replaces Prism regions.
/// </summary>
public sealed class ManApp : Application
{
    private const string MainContentRegion = "MainContentRegion";

    /// <summary>Runtime root container, also used to report startup failures in the shell.</summary>
    public static ServiceProvider? Services { get; private set; }

    private static MainWindowViewModel? _shellViewModel;

    public override void OnFrameworkInitializationCompleted()
    {
        // The WPF host is light-only; follow it instead of the OS dark/light preference.
        RequestedThemeVariant = ThemeVariant.Light;
        Styles.Add(new FluentTheme());

        Locale.MergeAssembly(typeof(ManApp).Assembly);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            MainWindow shell = new() { DataContext = Services!.GetRequiredService<MainWindowViewModel>() };
            desktop.MainWindow = shell;
            _shellViewModel = (MainWindowViewModel)shell.DataContext!;
            _ = StartAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task StartAsync()
    {
        try
        {
            if (Services is not null)
                await Services.InitSchemaRuntimeAsync();
        }
        catch (Exception ex)
        {
            ReportStartupFailure($"Schema runtime initialization failed: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Schema runtime initialization failed: {ex}");
            return;
        }

        try
        {
            await Services!.GetRequiredService<LanguageService>().InitializeAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Language initialization failed: {ex}");
        }

        Services!.GetRequiredService<IRegionManager>().RequestNavigate(
            MainContentRegion,
            nameof(TypeListView),
            result =>
            {
                if (!result.Success)
                    ReportStartupFailure(
                        $"Navigation to {nameof(TypeListView)} failed: {result.Exception?.Message ?? "unknown"}");
            });
    }

    private static void ReportStartupFailure(string message)
    {
        if (_shellViewModel is not null) _shellViewModel.StatusMessage = message;
        System.Diagnostics.Debug.WriteLine(message);
    }

    /// <summary>Builds the host container. Called once from <see cref="Program"/>.</summary>
    public static ServiceProvider BuildServices()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddAppSchemaAssembly<ManApp>();
        services.AddSchemaStorageProvider<DynamicAppEntryStorageProvider>();
        services.AddAppDataProvider<InMemoryAppDataProvider>();
        services.AddSchemaChat(options =>
        {
            options.Provider = ResolveSchemaChatProvider();
            ApplySchemaChatConfiguration(options);
            if (options.Provider is SchemaChatProvider.Ollama)
            {
                if (Environment.GetEnvironmentVariable("SCHEMA_CHAT_TIMEOUT_SECONDS") is not { Length: > 0 })
                    options.RequestTimeoutSeconds = 300;
                options.MaxOutputTokens ??= 2048;
            }
        });
        services.PrepareSchemaRuntime();

        ISchemaApiProvider? sharedRemote = CreateSharedRemote();

        // Host view registrations: struct fields / relations / enum values / app fields render as
        // tables through the registry instead of handwritten views.
        SchemaViewRegistry registry = ManSchemaViews.CreateRegistry();

        services.AddSingleton(registry);
        services.AddSingleton<SchemaContextFactory>(sp => new SchemaContextFactory(sp, sharedRemote));
        services.AddSingleton<ManViewFactory>();
        services.AddSingleton<RegionManager>();
        services.AddSingleton<IRegionManager>(sp => sp.GetRequiredService<RegionManager>());
        services.AddSingleton<DialogService>();
        services.AddSingleton<LanguageSettingsStore>();
        services.AddSingleton<LanguageService>();

        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<SchemaEditorViewModel>();
        services.AddTransient<TypeListViewModel>();
        services.AddTransient<TypeDocumentViewModel>();
        services.AddTransient<TypeDemoViewModel>();
        services.AddTransient<AppListViewModel>();
        services.AddTransient<AppDefinitionViewModel>();
        services.AddTransient<AppDemoViewModel>();
        services.AddTransient<AppFieldListViewModel>();
        services.AddTransient<AppFieldDefinitionViewModel>();
        services.AddTransient<AppFieldDemoViewModel>();
        services.AddTransient<AppWorkflowListViewModel>();
        services.AddTransient<FunctionDefinitionViewModel>();
        services.AddTransient<WorkflowConfigViewModel>();

        ServiceProvider provider = services.BuildServiceProvider();
        Services = provider;
        return provider;
    }

    private static SchemaChatProvider ResolveSchemaChatProvider()
    {
        string? value = Environment.GetEnvironmentVariable("SCHEMA_CHAT_PROVIDER");
        return System.Enum.TryParse(value, ignoreCase: true, out SchemaChatProvider provider)
            ? provider
            : SchemaChatProvider.Mock;
    }

    private static void ApplySchemaChatConfiguration(SchemaChatOptions options)
    {
        if (Environment.GetEnvironmentVariable("SCHEMA_CHAT_MODEL") is { Length: > 0 } model)
            options.ModelId = model;
        if (Environment.GetEnvironmentVariable("SCHEMA_CHAT_ENDPOINT") is { Length: > 0 } endpoint)
            options.Endpoint = endpoint;
        if (Environment.GetEnvironmentVariable("SCHEMA_CHAT_API_KEY") is { Length: > 0 } key)
            options.ApiKey = key;
        if (Environment.GetEnvironmentVariable("SCHEMA_CHAT_MAX_TOKENS") is { Length: > 0 } tokens
            && int.TryParse(tokens, out int parsed))
            options.MaxOutputTokens = parsed;
        if (Environment.GetEnvironmentVariable("SCHEMA_CHAT_TIMEOUT_SECONDS") is { Length: > 0 } timeout
            && int.TryParse(timeout, out int parsedTimeout))
            options.RequestTimeoutSeconds = parsedTimeout;
        if (Environment.GetEnvironmentVariable("SCHEMA_CHAT_THINKING") is { Length: > 0 } thinking
            && bool.TryParse(thinking, out bool thinkingEnabled))
            options.DisableThinking = !thinkingEnabled;
    }

    private static ISchemaApiProvider? CreateSharedRemote()
    {
        string? baseAddress = Environment.GetEnvironmentVariable("SCHEMA_API_BASE_URL");
        if (string.IsNullOrWhiteSpace(baseAddress)) return null;

        HttpClient http = new() { Timeout = TimeSpan.FromSeconds(30) };
        return new RemoteSchemaApiProvider(http, new SchemaApiProviderOptions { BaseAddress = baseAddress });
    }
}

/// <summary>Avalonia entry point.</summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        ServiceProvider services = ManApp.BuildServices();
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            services.Dispose();
        }
    }

    /// <summary>Builds the Avalonia application.</summary>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<ManApp>()
            .UsePlatformDetect()
            .LogToTrace();
}
