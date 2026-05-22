using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Quartz;
using SchemaNode.App.Components;
using SchemaNode.App.Http;
using SchemaNode.App.Http.JsonRpc;
using SchemaNode.Context;
using SchemaNode.Utility;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SchemaNode.App;

/// <summary>
/// App-layer DI registration and endpoint wiring.
/// </summary>
public static class AppInjection
{
    #region AddSchemaApp

    /// <summary>
    /// Registers all App-layer services using the <see cref="DefaultSchemaApiProtocol"/>.
    /// </summary>
    public static IServiceCollection AddSchemaApp(this IServiceCollection services,
        Action<SchemaAppConfig>? config = null,
        params Assembly[] assemblies)
        => services.AddSchemaApp<DefaultSchemaApiProtocol>(config, assemblies);

    /// <summary>
    /// Registers all App-layer services with the given protocol.
    /// </summary>
    public static IServiceCollection AddSchemaApp<TProtocol>(this IServiceCollection services,
        Action<SchemaAppConfig>? config = null,
        params Assembly[] assemblies)
        where TProtocol : class, ISchemaApiProtocol
    {
        var options = new SchemaAppConfig();
        config?.Invoke(options);
        SchemaAppConfig.Apply(options);

        // Logging
        services.TryAddSingleton<ILoggerFactory, LoggerFactory>();
        services.TryAddScoped(typeof(ILogger<>), typeof(Logger<>));

        // Critical region
        services.TryAddSingleton<ICriticalRegionProvider, LocalCriticalRegionProvider>();

        // Quartz scheduler
        services.AddQuartz(q =>
        {
            q.UseInMemoryStore();
            q.UseDefaultThreadPool(tp => tp.MaxConcurrency = options.MaxQuartzConcurrentThreads);
        });
        services.AddQuartzHostedService(opt => opt.WaitForJobsToComplete = true);

        // Schema context (Core)
        services.AddScoped<SchemaContext>();

        // Protocol
        services.PostConfigure<SwaggerGenOptions>(c => c.DocumentFilter<SchemaApiDocumentFilter>());
        services.TryAddTransient<ISchemaApiProtocol, TProtocol>();
        services.TryAddTransient<TProtocol>();

        // Access / context items
        services.AddScoped<Access>();
        services.AddScoped<AccessContextItemProvider>();

        // Workflow engine abstraction
        services.TryAddScoped<IWorkflowEngine, NullWorkflowEngine>();

        // Scan assemblies
        foreach (Assembly asm in assemblies) _registeredAssemblies.Add(asm);
        Assembly? entry = Assembly.GetEntryAssembly();
        if (entry != null) _registeredAssemblies.Add(entry);
        _registeredAssemblies.Add(typeof(TProtocol).Assembly);

        foreach (Assembly asm in _registeredAssemblies)
        {
            foreach (Type type in asm.GetTypes())
            {
                if (type.IsSubclassOfGenericType(typeof(SchemaApi<,>)) && !type.IsAbstract)
                {
                    Type apiBase    = type.GetGenericBaseType(typeof(SchemaApi<,>))!;
                    Type reqType    = apiBase.GetGenericArguments()[0];
                    Type resType    = apiBase.GetGenericArguments()[1];
                    bool noProtocol = type.GetCustomAttribute<NoProtocolAttribute>() != null;
                    _apiTypes.Add(new SchemaApiType(type, reqType, resType, noProtocol));
                    services.AddTransient(type);
                }
                else if (type.IsAssignableTo(typeof(ISchemaFormatProvider)) && type.IsClass && !type.IsAbstract)
                {
                    ISchemaFormatProvider.AddSchemaFormatProvider(type);
                }
            }
        }

        return services;
    }

    #endregion

    #region UseSchemaApis

    /// <summary>
    /// Maps all registered <see cref="SchemaApi{TRequest,TResponse}"/> endpoints and optionally serves the schema management UI.
    /// </summary>
    public static WebApplication UseSchemaApis(this WebApplication app,
        string prefix = "schema",
        string suffix = "",
        bool enableSchemaManage = false,
        Func<HttpContext, Task<bool>>? authorize = null)
    {
        _urlPrefix = prefix;
        _urlSuffix = suffix;

        ISchemaApiProtocol apiProtocol = app.Services.GetRequiredService<ISchemaApiProtocol>();

        foreach (var (apiType, url) in GetSchemaApis())
        {
            MethodInfo task = apiType.UseDefaultProtocol
                ? _processDefault.MakeGenericMethod(apiType.Api, apiType.Request, apiType.Response)
                : _processProtocol.MakeGenericMethod(apiType.Api, apiType.Request, apiType.Response);

            app.MapPost(url, async (HttpContext ctx) =>
            {
                Task<IResult> res = (Task<IResult>)task.Invoke(null, [ctx])!;
                return await res;
            });

            Console.WriteLine($"<{apiType.Api.Name}> is now listening on {url}.");
        }

        return app;
    }

    private static readonly MethodInfo _processProtocol =
        typeof(AppInjection).GetMethod(nameof(ProcessSchemaApiAsync), BindingFlags.Static | BindingFlags.NonPublic)!;

    private static readonly MethodInfo _processDefault =
        typeof(AppInjection).GetMethod(nameof(ProcessDefaultSchemaApiAsync), BindingFlags.Static | BindingFlags.NonPublic)!;

    private static async Task<IResult> ProcessSchemaApiAsync<TApi, TRequest, TResponse>(HttpContext ctx)
        where TApi : SchemaApi<TRequest, TResponse>
        where TRequest : SchemaApiRequest
        where TResponse : SchemaApiResponse
    {
        var protocol = ctx.RequestServices.GetRequiredService<ISchemaApiProtocol>();
        return await protocol.ProcessAsync<TApi, TRequest, TResponse>(ctx);
    }

    private static async Task<IResult> ProcessDefaultSchemaApiAsync<TApi, TRequest, TResponse>(HttpContext ctx)
        where TApi : SchemaApi<TRequest, TResponse>
        where TRequest : SchemaApiRequest
        where TResponse : SchemaApiResponse
    {
        ISchemaApiProtocol protocol = new DefaultSchemaApiProtocol();
        return await protocol.ProcessAsync<TApi, TRequest, TResponse>(ctx);
    }

    #endregion

    #region Internals

    internal static IEnumerable<(SchemaApiType ApiType, string Url)> GetSchemaApis()
    {
        foreach (var api in _apiTypes)
            yield return (api, GetRequestUrl(api.Request));
    }

    private static string GetRequestUrl(Type type)
    {
        string name = type.Name.EndsWith("request", StringComparison.OrdinalIgnoreCase)
            ? type.Name[..^"Request".Length]
            : type.Name;

        List<string> segs = [];
        int start = 0;
        for (int i = 1; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]))
            {
                segs.Add(name[start..i].ToLowerInvariant());
                start = i;
            }
        }
        segs.Add(name[start..].ToLowerInvariant());
        if (segs.Count == 0) segs.Add(name.ToLowerInvariant());

        string path = segs.Aggregate("", (a, b) => string.IsNullOrEmpty(a) ? b : $"{a}-{b}");
        string pre  = string.IsNullOrWhiteSpace(_urlPrefix) ? "" : $"{_urlPrefix}/";
        string suf  = string.IsNullOrWhiteSpace(_urlSuffix) ? "" : $".{_urlSuffix}";
        return $"{pre}{path}{suf}";
    }

    private static string _urlPrefix = "";
    private static string _urlSuffix = "";

    private static readonly HashSet<Assembly> _registeredAssemblies = [typeof(SchemaContext).Assembly];
    private static readonly List<SchemaApiType> _apiTypes = [];

    public record SchemaApiType(Type Api, Type Request, Type Response, bool UseDefaultProtocol);

    #endregion
}

/// <summary>
/// App-layer configuration options.
/// </summary>
public class SchemaAppConfig
{
    /// <summary>Max concurrent Quartz scheduler threads.</summary>
    public int MaxQuartzConcurrentThreads { get; set; } = 10;

    /// <summary>Default time zone ID.</summary>
    public string? TimeZone { get; set; }

    internal static SchemaAppConfig Current { get; private set; } = new();

    internal static void Apply(SchemaAppConfig config)
    {
        Current = config;
        if (!string.IsNullOrWhiteSpace(config.TimeZone))
            AccessContextItemProviderExtensions.SetDefaultTimeZone(config.TimeZone);
    }
}

/// <summary>
/// Null/no-op workflow engine — replace with a real implementation via DI.
/// </summary>
internal sealed class NullWorkflowEngine : IWorkflowEngine
{
    public Task ActivateAsync(AppWorkflowType workflow, IAppSchemaContext context) => Task.CompletedTask;
    public Task DeactivateAsync(AppWorkflowType workflow) => Task.CompletedTask;
}
