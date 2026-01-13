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
using SchemaNode.Function;
using SchemaNode.Http;
using SchemaNode.Runtime;
using SchemaNode.Utility;
using Swashbuckle.AspNetCore.SwaggerGen;
using static SchemaNode.Utility.Schema;
using static SchemaNode.Utility.App;
using static SchemaNode.Utility.Constant;
// ReSharper disable MemberCanBePrivate.Global

namespace SchemaNode;

public static class Injection
{
    #region Schemas

    public static IServiceCollection AddSchemaAssemblies(this IServiceCollection services, params Assembly[] assemblies)
    {
        foreach (Assembly assembly in assemblies) RegisterAssemblys.Add(assembly);
        return services;
    }

    public static IServiceCollection AddSchemaNode(this IServiceCollection services, Action<SchemaNodeConfig>? config = null, params Assembly[] assemblies)
    {
        return services.AddSchemaNode<DefaultSchemaApiProtocol>(config, assemblies);
    }
    
    /// <summary>
    /// Use the schema context with config
    /// </summary>
    public static IServiceCollection AddSchemaNode<T>(this IServiceCollection services, Action<SchemaNodeConfig>? config = null, params Assembly[] assemblies)
        where T : class, ISchemaApiProtocol
    {
        if (config != null)
        {
            config.Invoke(SchemaContext.Config);
            SystemDate.SetTimeZone(SchemaContext.Config.TimeZone);
        }
        
        // default logger
        services.TryAddSingleton<ILoggerFactory, LoggerFactory>();
        services.TryAddScoped(typeof(ILogger<>), typeof(Logger<>));
        
        // critical region
        services.TryAddSingleton<ICriticalRegionProvider, LocalCriticalRegionProvider>();

        // Quartz scheduler
        services.AddQuartz(q =>
        {
            q.UseInMemoryStore();

            q.UseDefaultThreadPool(tp =>
            {
                tp.MaxConcurrency = SchemaContext.Config.MaxQuartzConcurrentThreads;
            });
        });

        services.AddQuartzHostedService(opt =>
        {
            opt.WaitForJobsToComplete = true;
        });
        
        // The schema context
        services.AddScoped<SchemaContext>();
        services.AddTransient<WorkflowContext>();

        // Expression visitor
        services.AddSingleton<IExpressionVisitor, ArithmeticExpressionVisitor>();
        services.AddSingleton<IExpressionVisitor, BreakExpTypeVisitor>();
        services.AddSingleton<IExpressionVisitor, CollectionExpressionVisitor>();
        services.AddSingleton<IExpressionVisitor, ConditionalExpressionVisitor>();
        services.AddSingleton<IExpressionVisitor, ConstantExpressionVisitor>();
        services.AddSingleton<IExpressionVisitor, DataSourceExpressionVisitor>();
        services.AddSingleton<IExpressionVisitor, FieldAccessExpressionVisitor>();
        services.AddSingleton<IExpressionVisitor, LogicExpressionVisitor>();

        // api protocol
        services.PostConfigure<SwaggerGenOptions>(c => c.DocumentFilter<SchemaApiDocumentFilter>());
        services.TryAddTransient<ISchemaApiProtocol, T>();
        services.TryAddTransient<T>();
        
        // Register schema assemblies
        foreach (Assembly assembly in assemblies) RegisterAssemblys.Add(assembly);
        Assembly? entryAssembly = Assembly.GetEntryAssembly();
        if (entryAssembly != null) RegisterAssemblys.Add(entryAssembly);
        RegisterAssemblys.Add(typeof(T).Assembly);

        // Register system schema types and apis
        foreach (Assembly assembly in RegisterAssemblys)
        {
            SchemaAttribute? rootNamespaceAttr = assembly.GetCustomAttribute<SchemaAttribute>();
            if (rootNamespaceAttr != null)
            {
                string name = rootNamespaceAttr.Name ?? assembly.GetName().Name ?? "";
                string[] displaySeg = rootNamespaceAttr.Display?.Key.Split('.') ?? [];
                string[] nameSeg = name.Split('.');
                
                if (displaySeg.Length > 1 && displaySeg.Length == nameSeg.Length)
                {
                    // multi-level namespace
                    for (int i = 0; i < nameSeg.Length; i++)
                    {
                        SaveSystemNodeSchema(new NodeSchema
                        {
                            Name = string.Join('.', nameSeg[..(i + 1)]),
                            Type = SchemaType.Namespace,
                            Display = displaySeg[i],
                        });
                    }
                }
                else
                {
                    SaveSystemNodeSchema(new NodeSchema
                    {
                        Name = name,
                        Type = SchemaType.Namespace,
                        Display = rootNamespaceAttr.Display,
                    });
                }
            }
            
            SchemaAppAttribute? appAttr = assembly.GetCustomAttribute<SchemaAppAttribute>();
            string appName = assembly.GetName().Name?.ToLower() ?? "app";
            if (appAttr?.Application != null)
            {
                appName = appAttr.Application;
                string[] displaySeg = appAttr.Display?.Split('.') ?? [];
                string[] nameSeg = appName.Split('.');
                if (displaySeg.Length > 1 && displaySeg.Length == nameSeg.Length)
                {
                    for (int i = 0; i < nameSeg.Length; i++)
                    {
                        SaveSystemAppField(string.Join('.', nameSeg[..(i + 1)]), display: displaySeg[i]);
                    }
                }
                else
                {
                    SaveSystemAppField(appAttr.Application, display: appAttr.Display);
                }
            }

            // scan all
            foreach (var type in assembly.GetTypes())
            {
                if (type.IsSubclassOfGenericType(typeof(SchemaApi<,>)))
                {
                    // Schema api
                    if (!type.IsAbstract)
                    {
                        Type apiBaseType = type.GetGenericBaseType(typeof(SchemaApi<,>))!;
                        Type requestType = apiBaseType.GetGenericArguments()[0];
                        Type responseType = apiBaseType.GetGenericArguments()[1];

                        ApiTypes.Add(new SchemaApiType(type, requestType, responseType, type.GetCustomAttribute<NoProtocolAttribute>() != null));
                        services.AddTransient(type);
                    }
                }
                else
                {
                    // schema type
                    string? typeName = type.GetSchemaType();

                    // auto application registered
                    if (typeName != null && (type is { IsClass: true, IsAbstract: false } ||
                                             type is { IsValueType: true, IsEnum: false } && !type.IsPrimitiveLike()))
                    {
                        SchemaAppAttribute? attr = type.GetCustomAttribute<SchemaAppAttribute>();
                        if (attr != null)
                        {
                            string fieldName = attr.Field ?? type.Name.ToLower();
                            string application = attr.Application ?? appName;
                            SaveSystemAppField(application, new AppFieldSchema
                            {
                                Name = fieldName,
                                Type = type.GetProperties().Any(p => p.GetCustomAttributes<IndexAttribute>().Any())
                                    ? $"{typeName}s"
                                    : typeName,
                                Display = attr.Display ?? type.GetSummaryFromXmlDoc() ?? fieldName,
                                IncrUpdate = attr.IncrUpdate,
                            }, type: type);
                        }
                    }
                }
            }
        }
        
        // event dispatcher
        services.TryAddSingleton<IEventDispatcher<Event>, DefaultEventDispatcher>();

        // workflow scheduler
        services.TryAddSingleton<IWorkflowScheduler, DefaultWorkflowScheduler>();
        
        // workflow persistence
        services.TryAddScoped<IWorkflowContextPersistence, DynamicWorkflowContextPersistence>();
        
        // Register system.context
        NodeSchema contextSchema = NewSystemStruct(NS_SYSTEM_CONTEXT, []);
        services.AddScoped<Access>();
        services.AddScoped<AccessContextItemProvider>();

        // context item scan
        foreach(ServiceDescriptor desc in services)
        {
            Type providerType = desc.ServiceType;
            if (providerType.GetInterfaces().FirstOrDefault(i 
                    => i.IsSubclassOfGenericType(typeof(ISchemaContextItemProvider<>))) is { } @interface)
            {
                Type itemType = @interface.GetGenericArguments()[0];
                string? schemaType = itemType.GetSchemaType(true, providerType.Assembly.GetCustomAttribute<SchemaAttribute>()?.Name);
                if (string.IsNullOrEmpty(schemaType)) continue;

                // use the last part as field name
                string field = schemaType.SplitTypeName().Last().ToLower();
                contextSchema.Struct!.Fields = contextSchema.Struct!.Fields.Append(new StructFieldConfig
                {
                    Name = field,
                    Type = schemaType,
                    Display = $"{{@{schemaType}}}",
                }).ToArray();

                // record the binding
                SchemaContextItemExtension.BindSchemaContextItemProvider(field, schemaType, providerType, itemType);
            }
        }
        
        // Add the system.context
        SaveSystemNodeSchema(contextSchema);
        
        // Init the Schema Context
        using IServiceScope scope = services.BuildServiceProvider().CreateScope();
        SchemaContext context = scope.ServiceProvider.GetRequiredService<SchemaContext>();
        context.InitSystemContextAsync().GetAwaiter().GetResult();
        
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
        where T : class, IAppDataProvider
    {
        services.TryAddScoped<IAppDataProvider, T>();
        if (typeof(ISchemaStorageProvider).IsAssignableFrom(typeof(T)))
            services.TryAdd(new ServiceDescriptor(typeof(ISchemaStorageProvider), typeof(T), ServiceLifetime.Scoped));
        
        // sql provider check
        Type? interfaceType = typeof(T).GetInterfaces().FirstOrDefault(i => i.IsSubclassOfGenericType(typeof(IAppDataSqlProvider<>)));
        if (interfaceType != null)
        {
            // keep it simple, just set it
            ISqlProvider instance = (ISqlProvider)Activator.CreateInstance(interfaceType.GetGenericArguments()[0])!;
            services.AddSingleton(instance);
            DynamicTableSchema.SqlProvider = instance;
        }
        
        return services.AddScoped<T>();
    }
    
    /// <summary>
    /// Pre-load all schema nodes
    /// </summary>
    public static WebApplication PreLoadSchemaNodes(this WebApplication app)
    {
        app.Lifetime.ApplicationStarted.Register(async void () =>
        {
            using IServiceScope scope = app.Services.CreateScope();
            SchemaContext context = scope.ServiceProvider.GetRequiredService<SchemaContext>();
            
            // preload schema and app types·
            context.LogInformation("[Preload] Loading schema ...");
            context.ResetTypeNamespace();
            await context.GetSchemaTypeAsync("", preload: true);
            
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
        });
        return app;
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
        bool hasSchemaStorage = service.IsService(typeof(ISchemaStorageProvider));
        bool hasAppDataStorage = service.IsService(typeof(IAppDataProvider));
        
        ISchemaApiProtocol apiProtocol = app.Services.GetRequiredService<ISchemaApiProtocol>();
        var protocolMeta = apiProtocol.GetProtocolMeta(app.Services);

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

                // 在 <head> 中插入 meta 标签
                html = html.Replace("</head>", string.Join("", [
                    "<meta name=\"schema-embedded\" content=\"true\">",
                    $"<meta name=\"schema-api-base-url\" content=\"/{prefix}\">",
                    $"<meta name=\"schema-api-protocol\" content='{protocolMeta.ToJson()}'></head>"]));
    
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
