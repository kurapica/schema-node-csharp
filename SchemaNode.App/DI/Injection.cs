using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SchemaNode.Attribute;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Schema;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Quartz;
using SchemaNode.Components.Context;
using SchemaNode.Http;
using SchemaNode.Runtime;
using SchemaNode.Utility;
using Swashbuckle.AspNetCore.SwaggerGen;
using static SchemaNode.Utility.Schema;
using static SchemaNode.Utility.App;
using static SchemaNode.Utility.Constant;

namespace SchemaNode;

public static class Injection
{
    #region Schemas
    
    /// <summary>
    /// Pre-load all schema nodes
    /// </summary>
    public static WebApplication PreLoadSchemaNodes(this WebApplication app)
    {
        app.Lifetime.ApplicationStarted.Register(async void () =>
        {
            try
            {
                using IServiceScope scope = app.Services.CreateScope();
                SchemaContext context = scope.ServiceProvider.GetRequiredService<SchemaContext>();
            
                // preload schema and app types·
                context.LogInformation("[Preload] Loading schema ...");
                context.ResetTypeNamespace();
                await context.GetNodeTypeAsync("", preload: true);
            
                context.LogInformation("[Preload] Loading application ...");
                context.ResetAppContainer();
                await context.GetAppTypeAsync("", preload: true);
            
                // re-compile function types
                FunctionType[] funcs = ReCompileFuncTypes?.ToArray() ?? [];
                ReCompileFuncTypes?.Clear();
            
                if (funcs.Length > 0)
                    context.LogInformation($"Re compiling {funcs.Length} function types ...");

                int old = 0;
                while (old != funcs.Length)
                {
                    foreach (var funcType in funcs)
                    {
                        context.LogInformation($"Re compiling function type: {funcType.Name}");
                        funcType.Status = SchemaNodeStatus.Ready;
                        await funcType.PreCompileAsync(context);

                        if (funcType.Status == SchemaNodeStatus.Ready) continue;
                        ReCompileFuncTypes ??= [];
                        ReCompileFuncTypes.Add(funcType);
                    }
                    old = funcs.Length;
                    funcs = ReCompileFuncTypes?.ToArray() ?? [];
                    ReCompileFuncTypes?.Clear();
                }
                ReCompileFuncTypes = null;
            
                // start work flows
                context.LogInformation("[Preload] Starting workflows ...");
                foreach(AppWorkflowType workflow in WorkflowTypes ?? [])
                {
                    try
                    {
                        context.LogInformation($"Starting workflow: {workflow.Name}");
                        await workflow.LoadAsync(context);
                    }
                    catch (Exception ex)
                    {
                        context.LogError(ex, $"Failed to start workflow: {workflow.Name}, error: {ex.Message}");
                    }
                }
                WorkflowTypes = null;

                // start event source
                context.LogInformation("[Preload] Starting event sources ...");
                foreach(IEventSource eventSource in app.Services.GetServices<IEventSource>())
                {
                    try
                    {
                        context.LogInformation($"Starting event source: {eventSource.GetType().FullName}");
                        await eventSource.StartAsync(context, app.Lifetime.ApplicationStopping);
                    }
                    catch (Exception ex)
                    {
                        context.LogError(ex, $"Failed to start event source: {eventSource.GetType().FullName}, error: {ex.Message}");
                    }
                }
            
                // Preload completed
                context.LogInformation("[Preload] Completed, starting service.");
            }
            catch (Exception e)
            {
                Console.WriteLine($"[Preload] Failed: {e.Message}");
            }
        });
        return app;
    }
    
    public static async Task<IServiceProvider> PreLoadSchemaNodes(this IServiceProvider provider)
    {
        using IServiceScope scope = provider.CreateScope();
        SchemaContext context = scope.ServiceProvider.GetRequiredService<SchemaContext>();
    
        // preload schema and app types·
        context.LogInformation("[Preload] Loading schema ...");
        context.ResetTypeNamespace();
        await context.GetNodeTypeAsync("", preload: true);
    
        context.LogInformation("[Preload] Loading application ...");
        context.ResetAppContainer();
        await context.GetAppTypeAsync("", preload: true);
    
        // re-compile function types
        FunctionType[] funcs = ReCompileFuncTypes?.ToArray() ?? [];
        ReCompileFuncTypes?.Clear();
    
        if (funcs.Length > 0)
            context.LogInformation($"Re compiling {funcs.Length} function types ...");

        int old = 0;
        while (old != funcs.Length)
        {
            foreach (var funcType in funcs)
            {
                context.LogInformation($"Re compiling function type: {funcType.Name}");
                funcType.Status = SchemaNodeStatus.Ready;
                await funcType.PreCompileAsync(context);

                if (funcType.Status == SchemaNodeStatus.Ready) continue;
                ReCompileFuncTypes ??= [];
                ReCompileFuncTypes.Add(funcType);
            }
            old = funcs.Length;
            funcs = ReCompileFuncTypes?.ToArray() ?? [];
            ReCompileFuncTypes?.Clear();
        }
        ReCompileFuncTypes = null;
    
        // start work flows
        context.LogInformation("[Preload] Starting workflows ...");
        foreach(AppWorkflowType workflow in WorkflowTypes ?? [])
        {
            try
            {
                context.LogInformation($"Starting workflow: {workflow.Name}");
                await workflow.LoadAsync(context);
            }
            catch (Exception ex)
            {
                context.LogError(ex, $"Failed to start workflow: {workflow.Name}, error: {ex.Message}");
            }
        }
        WorkflowTypes = null;
    
        // Preload completed
        context.LogInformation("[Preload] Completed, starting service.");
        return provider;
    }
    
    internal static ConcurrentBag<FunctionType>? ReCompileFuncTypes = [];
    internal static ConcurrentBag<AppWorkflowType>? WorkflowTypes = [];
    
    #endregion

    #region Schema Apis
    
    /// <summary>
    /// Enable schema apis
    /// </summary>
    /// <param name="app">The web application</param>
    /// <param name="prefix">the api prefix, default "schema"</param>
    /// <param name="suffix">the api suffix, like "action"</param>
    /// <param name="enableAppDataApi">Whether enable application data api for test</param>
    /// <param name="enableSchemaManage">Whether enable the embed schema management website</param>
    /// <param name="authorize">The authorization func</param>
    /// <param name="managePolicy">The authorization policy</param>
    /// <returns></returns>
    public static WebApplication UseSchemaApis(this WebApplication app, string prefix = "schema", string suffix = "", bool enableAppDataApi = false, bool enableSchemaManage = false,
        Func<HttpContext, Task<bool>>? authorize = null,
        string? managePolicy = null)
    {
        UrlPrefix = prefix;
        UrlSuffix = suffix;
        
        // may disable some apis in this assembly
        Assembly schemaAssembly = typeof(Injection).Assembly;

        IServiceProviderIsService service = app.Services.GetRequiredService<IServiceProviderIsService>();
        bool hasSchemaStorage = service.IsService(typeof(IAppSchemaStorageProvider));
        bool hasAppDataStorage = service.IsService(typeof(IAppDataProvider));
        
        ISchemaApiProtocol apiProtocol = app.Services.GetRequiredService<ISchemaApiProtocol>();

        foreach ((SchemaApiType apiType, string url)  in GetSchemaApis())
        {            
            if (apiType.Api.Assembly == schemaAssembly)
            {
                // no storage no edit
                if (apiType.Api.FullName!.Contains("api.schema.edit", StringComparison.OrdinalIgnoreCase))
                {
                    if (!hasSchemaStorage) continue;
                }
                else if (apiType.Api.FullName!.Contains("api.schema.application", StringComparison.OrdinalIgnoreCase))
                {
                    if (!hasAppDataStorage || !enableAppDataApi) continue;
                }
            }
            
            MethodInfo task = apiType.UseDefaultProtocol 
                ? typeof(Injection).GetMethod(nameof(ProcessDefaultSchemaApiAsync),BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(apiType.Api, apiType.Request, apiType.Response)
                : typeof(Injection).GetMethod(nameof(ProcessSchemaApiAsync),BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(apiType.Api, apiType.Request, apiType.Response);
            app.MapPost(url, async (HttpContext ctx) =>
            {
                Task<IResult> res = (Task<IResult>)task.Invoke(null, [ctx])!;
                return await res;
            });
            Console.WriteLine($"<{apiType.Api.Name}> is now listening.");
        }
        
        if (enableSchemaManage)
        {
            // schema manage web sites
            var manProvider = new CachedZipFileProvider(typeof(SchemaApi<,>).Assembly, "SchemaNode.www.zip", "dist");

            var endpoint = app.MapGet("/schema-node-man", async context =>
            {
                // authorization
                if (authorize is not null)
                {
                    var ok = await authorize(context);
                    if (!ok)
                    {
                        context.Response.StatusCode = 403;
                        await context.Response.WriteAsync("Forbidden");
                        return;
                    }
                }

                // add embedded meta
                await using var stream = manProvider.GetFileInfo("index.html").CreateReadStream();
                using var reader = new StreamReader(stream);
                var html = await reader.ReadToEndAsync();

                // add <head> meta tag
                html = html.Replace("</head>", string.Join("", [
                    "<meta name=\"schema-embedded\" content=\"true\">",
                    $"<meta name=\"schema-api-base-url\" content=\"/{prefix}\">",
                    $"<meta name=\"schema-api-protocol\" content='{apiProtocol.GetProtocolMeta(app.Services).ToJson()}'></head>"]));
    
                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync(html);
            });
            
            // add authorization policy
            if (!string.IsNullOrEmpty(managePolicy))
                endpoint.RequireAuthorization(managePolicy);
            
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = manProvider,
                RequestPath = "/schema-node-man"
            });
            
        }
        
        return app;
    }
    
    static async Task<IResult> ProcessSchemaApiAsync<TApi, TRequest, TResponse>(HttpContext ctx) 
        where TApi: SchemaApi<TRequest, TResponse>
        where TRequest: SchemaApiRequest
        where TResponse: SchemaApiResponse
    {
        var protocol = ctx.RequestServices.GetRequiredService<ISchemaApiProtocol>();
        return await protocol.ProcessAsync<TApi, TRequest, TResponse>(ctx);
    }
 
    static async Task<IResult> ProcessDefaultSchemaApiAsync<TApi, TRequest, TResponse>(HttpContext ctx) 
        where TApi: SchemaApi<TRequest, TResponse>
        where TRequest: SchemaApiRequest
        where TResponse: SchemaApiResponse
    {
        ISchemaApiProtocol protocol = new DefaultSchemaApiProtocol();
        return await protocol.ProcessAsync<TApi, TRequest, TResponse>(ctx);
    }
    
    /// <summary>
    /// Gets all apis
    /// </summary>
    /// <returns></returns>
    internal static IEnumerable<(SchemaApiType, string url)> GetSchemaApis()
    {
        foreach(var api in ApiTypes)
        {
            yield return (api, GetRequestUrl(api.Request));
        }
    }

    static string GetRequestUrl(Type type)
    {
        string name = type.Name.EndsWith("request", StringComparison.OrdinalIgnoreCase)
            ? type.Name[..^"Request".Length]
            : type.Name;
        List<string> nameSegments = new();
        int nameSegmentStartIndex = 0;
        for (int i = 1; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]))
            {
                nameSegments.Add(name[nameSegmentStartIndex..i].ToLowerInvariant());
                nameSegmentStartIndex = i;
            }
        }
        // Add tail
        nameSegments.Add(name[nameSegmentStartIndex..].ToLowerInvariant());
        if (!nameSegments.Any())
            nameSegments.Add(name.ToLowerInvariant());
        return $"{(string.IsNullOrWhiteSpace(UrlPrefix) ? "" : $"{UrlPrefix}/")}{nameSegments.Aggregate(string.Empty, (segment0, segment) => !string.IsNullOrEmpty(segment0) ? $"{segment0}-{segment}" : segment)}{(string.IsNullOrWhiteSpace(UrlSuffix) ? "" : $".{UrlSuffix}")}";
    }

    /// <summary>
    /// Url prefix
    /// </summary>
    static string UrlPrefix { get; set; } = "";

    /// <summary>
    /// Url suffix
    /// </summary>
    static string UrlSuffix { get; set; } = "";

    static readonly HashSet<Assembly> RegisterAssemblys = [typeof(SchemaContext).Assembly];
    static readonly List<SchemaApiType> ApiTypes = new();

    public record SchemaApiType(Type Api, Type Request, Type Response, bool UseDefaultProtocol);

    #endregion

    #region Methods

    /// <summary>
    /// Gets all registered schema assemblies
    /// </summary>
    public static IEnumerable<Assembly> GetRegisteredAssemblies() => RegisterAssemblys;

    #endregion
}
