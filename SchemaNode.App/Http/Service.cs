using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Data;
using SchemaNode.Schema.Provider;
using SchemaNode.Utility;
using System.Reflection;

namespace SchemaNode.Http;

public static class Service
{
    /// <summary>
    /// Sets the api protocol
    /// </summary>
    public static IServiceCollection WithSchemaApiProtocol<T>(this IServiceCollection services) where T: class, ISchemaApiProtocol
    {
        services.AddTransient<ISchemaApiProtocol, T>();
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

        // may disable some apis in this assembly
        Assembly schemaAssembly = typeof(Service).Assembly;

        IServiceProviderIsService service = app.Services.GetRequiredService<IServiceProviderIsService>();
        bool hasSchemaStorage = service.IsService(typeof(IAppSchemaStorageProvider));
        bool hasAppDataStorage = service.IsService(typeof(IAppDataProvider));

        ISchemaApiProtocol apiProtocol = app.Services.GetRequiredService<ISchemaApiProtocol>();

        foreach ((SchemaApiType apiType, string url) in GetSchemaApis())
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
                ? typeof(Service).GetMethod(nameof(ProcessDefaultSchemaApiAsync), BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(apiType.Api, apiType.Request, apiType.Response)
                : typeof(Service).GetMethod(nameof(ProcessSchemaApiAsync), BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(apiType.Api, apiType.Request, apiType.Response);
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
        where TApi : SchemaApi<TRequest, TResponse>
        where TRequest : SchemaApiRequest
        where TResponse : SchemaApiResponse
    {
        var protocol = ctx.RequestServices.GetRequiredService<ISchemaApiProtocol>();
        return await protocol.ProcessAsync<TApi, TRequest, TResponse>(ctx);
    }

    static async Task<IResult> ProcessDefaultSchemaApiAsync<TApi, TRequest, TResponse>(HttpContext ctx)
        where TApi : SchemaApi<TRequest, TResponse>
        where TRequest : SchemaApiRequest
        where TResponse : SchemaApiResponse
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
        foreach (var api in ApiTypes)
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

    internal static void AddApiType(Type type)
    {
        // Schema api
        Type apiBaseType = type.GetGenericBaseType(typeof(SchemaApi<,>))!;
        Type requestType = apiBaseType.GetGenericArguments()[0];
        Type responseType = apiBaseType.GetGenericArguments()[1];

        ApiTypes.Add(new SchemaApiType(type, requestType, responseType,
            type.GetCustomAttribute<NoProtocolAttribute>() != null));
    }

    static readonly List<SchemaApiType> ApiTypes = new();
    internal record SchemaApiType(Type Api, Type Request, Type Response, bool UseDefaultProtocol);
}
