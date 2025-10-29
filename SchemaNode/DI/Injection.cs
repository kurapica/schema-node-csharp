using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SchemaNode.Attribute;
using SchemaNode.Components;
using SchemaNode.Components.Provider;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Schema;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using SchemaNode.Function;
using SchemaNode.Http;
using SchemaNode.Utility;
using Swashbuckle.AspNetCore.SwaggerGen;
using static SchemaNode.Utility.Schema;
using static SchemaNode.Utility.App;
// ReSharper disable MemberCanBePrivate.Global

namespace SchemaNode;

public static class Injection
{
    #region Schemas
    
    /// <summary>
    /// Use the schema context with config
    /// </summary>
    public static IServiceCollection AddSchemaNode(this IServiceCollection services, Action<SchemaNodeConfig>? config = null)
    {
        if (config != null)
        {
            config.Invoke(SchemaContext.Config);
            SystemDate.SetTimeZone(SchemaContext.Config.TimeZone);
        }
        
        // default logger
        services.TryAddSingleton<ILoggerFactory, LoggerFactory>();
        services.TryAddScoped(typeof(ILogger<>), typeof(Logger<>));
        
        // message handlers
        SchemaMessageHandlerExtensions.RegisterSchemaMessageHandlers<SchemaContext>(services);
        SchemaMessageHandlerExtensions.RegisterSchemaMessageHandlers(services, Assembly.GetEntryAssembly());

        // critical region
        services.TryAddSingleton<ICriticalRegionProvider, LocalCriticalRegionProvider>();

        // The schema context
        services.AddScoped<SchemaContext>();
        
        // system.schema types
        services.AddSchemaSystemTypes<SchemaContext>();
        services.AddSchemaSystemTypes(Assembly.GetEntryAssembly());
        
        return services;
    }
    
    /// <summary>
    /// Register the schema provider
    /// </summary>
    public static IServiceCollection AddSchemaProvider<T>(this IServiceCollection services) 
        where T : class, ISchemaProvider
    {
        return services.AddScoped<ISchemaProvider, T>().AddScoped<T>();
    }

    /// <summary>
    /// Register the schema storage provider
    /// </summary>
    public static IServiceCollection AddSchemaStorageProvider<T>(this IServiceCollection services)
        where T : class, ISchemaStorageProvider
    {
        services.TryAddScoped<ISchemaStorageProvider, T>();
        return services.AddScoped<ISchemaProvider, T>().AddScoped<T>();
    }

    /// <summary>
    /// Register app schema data provider
    /// </summary>
    /// <param name="services"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static IServiceCollection AddAppSchemaDataProvider<T>(this IServiceCollection services)
        where T : class, IAppSchemaDataProvider
    {
        services.TryAddScoped<IAppSchemaDataProvider, T>();
        if (typeof(ISchemaStorageProvider).IsAssignableFrom(typeof(T)))
            services.TryAdd(new ServiceDescriptor(typeof(ISchemaStorageProvider), typeof(T), ServiceLifetime.Scoped));
        return services.AddScoped<T>();
    }
    
    /// <summary>
    /// Register system types from the assembly that contains the given type
    /// </summary>
    public static IServiceCollection AddSchemaSystemTypes<T>(this IServiceCollection services)
    {
        return AddSchemaSystemTypes(services, typeof(T));
    }

    /// <summary>
    /// Register system types from the assembly that contains the given type
    /// </summary>
    public static IServiceCollection AddSchemaSystemTypes(this IServiceCollection services, Type type)
    {
        return AddSchemaSystemTypes(services, type.Assembly);
    }
    
    /// <summary>
    /// Register system types from the assembly that contains the given type
    /// </summary>
    public static IServiceCollection AddSchemaSystemTypes(this IServiceCollection services, Assembly? assembly)
    {
        if (assembly == null) return services;
        
        SchemaTypeAttribute? rootNamespaceAttr = assembly.GetCustomAttribute<SchemaTypeAttribute>();
        if (rootNamespaceAttr != null)
        {
            SaveSystemNodeSchema(new NodeSchema
            {
                Name = rootNamespaceAttr.Name ?? assembly.GetName().Name ?? "",
                Type = SchemaType.Namespace,
                Display = rootNamespaceAttr.Display,
            });
        }
        
        SchemaAppAttribute? appAttr = assembly.GetCustomAttribute<SchemaAppAttribute>();
        string appName = assembly.GetName().Name?.ToLower() ?? "app";
        if (appAttr?.Application != null)
        {
            appName = appAttr.Application;
            SaveSystemAppField(appAttr.Application, display: appAttr.Display);
        }

        // scan all
        foreach (var type in assembly.GetTypes())
        {
            string? typeName = type.GetSchemaType();
            
            // auto application registered
            if (typeName != null && (type is { IsClass: true, IsAbstract: false } || type is { IsValueType: true, IsEnum: false } && !type.IsPrimitiveLike() ))
            {
                SchemaAppAttribute? attr = type.GetCustomAttribute<SchemaAppAttribute>();
                if (attr != null)
                {
                    string fieldName = attr.Field ?? type.Name.ToLower();
                    string application = attr.Application ?? appName;
                    SaveSystemAppField(application, new AppFieldSchema
                    {
                        Name = fieldName,
                        Type = type.GetProperties().Any(p => p.GetCustomAttributes<IndexAttribute>().Any()) ? $"{typeName}s" : typeName,
                        Display = attr.Display,
                        IncrUpdate = attr.IncrUpdate,
                    }, type: type);
                }
            }
        }

        return services;
    }

    /// <summary>
    /// Pre-load all schema nodes
    /// </summary>
    public static IApplicationBuilder PreLoadSchemaNodes(this IApplicationBuilder app)
    {
        _ = Task.Run(async() =>
        {
            using IServiceScope scope = app.ApplicationServices.CreateScope();
            SchemaContext context = scope.ServiceProvider.GetRequiredService<SchemaContext>();
            await context.GetSchemaTypeAsync("", preload: true);
            await context.GetAppTypeAsync("", preload: true);
        });
        return app;
    }
    
    #endregion

    #region Schema Apis
    
    /// <summary>
    /// Add schema apis in an assembly, the entry assembly and SchemaNode will be added automatically
    /// </summary>
    public static IServiceCollection AddSchemaApis(this IServiceCollection services, Assembly assembly)
    {
        if (!RegisterAssemblys.Add(assembly)) return services;
        
        foreach (Type type in assembly!.GetTypes().Where(t => t.IsSubclassOfGenericType(typeof(SchemaApi<,>)) && !t.IsAbstract))
        {
            Type apiBaseType = type.GetGenericBaseType(typeof(SchemaApi<,>))!;
            Type requestType = apiBaseType.GetGenericArguments()[0];
            Type responseType = apiBaseType.GetGenericArguments()[1];

            ApiTypes.Add(new SchemaApiType(type, requestType, responseType));
            services.AddTransient(type);
        }
    
        return services;
    }

    /// <summary>
    /// Register the default schema api processor
    /// </summary>
    public static IServiceCollection AddSchemaApis(this IServiceCollection services) 
    {
        return AddSchemaApis<DefaultSchemaApiProtocol>(services);
    }
    
    /// <summary>
    /// Register the schema api processor
    /// </summary>
    public static IServiceCollection AddSchemaApis<T>(this IServiceCollection services) 
        where T : class, ISchemaApiProtocol
    {
        Assembly d = typeof(Injection).Assembly;
        AddSchemaApis(services, d);
        AddSchemaApis(services, Assembly.GetEntryAssembly() ?? d);
        AddSchemaApis(services, typeof(T).Assembly);
        
        services.PostConfigure<SwaggerGenOptions>(c => c.DocumentFilter<SchemaApiDocumentFilter>());
        services.TryAddTransient<ISchemaApiProtocol, T>();
        services.TryAddTransient<T>();
        return services;
    }
    
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
        
        // add default Assembly
        Assembly schemaAssembly = typeof(Injection).Assembly;

        IServiceProviderIsService service = app.Services.GetRequiredService<IServiceProviderIsService>();
        bool hasSchemaStorage = service.IsService(typeof(ISchemaStorageProvider));
        bool hasAppDataStorage = service.IsService(typeof(IAppSchemaDataProvider));

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
            
            MethodInfo task = typeof(Injection).GetMethod(nameof(ProcessSchemaApiAsync),BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(apiType.Api, apiType.Request, apiType.Response);
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

                // 在 <head> 中插入 meta 标签
                html = html.Replace("</head>", $"<meta name=\"schema-embedded\" content=\"true\"><meta name=\"api-base-url\" content=\"/{prefix}\"></head>");
    
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
        var processor = ctx.RequestServices.GetRequiredService<ISchemaApiProtocol>();
        return await processor.ProcessAsync<TApi, TRequest, TResponse>(ctx);
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

    static readonly HashSet<Assembly> RegisterAssemblys = new();
    static readonly List<SchemaApiType> ApiTypes = new();
    public record SchemaApiType(Type Api, Type Request, Type Response);

    #endregion
}
