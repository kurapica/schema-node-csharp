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
using SchemaNode.Function;
using SchemaNode.Utility;
using static SchemaNode.Utility.Schema;
using static SchemaNode.Utility.App;

namespace SchemaNode;

public static class Injection
{
    /// <summary>
    /// Use the schema context with config
    /// </summary>
    public static IServiceCollection AddSchemaNode(this IServiceCollection services, Action<SchemaNodeConfig>? config = null)
    {
        if (config != null)
        {
            SchemaNodeConfig nodeConfig = new SchemaNodeConfig();
            config.Invoke(nodeConfig);
            services.AddSingleton(nodeConfig);
            SystemDate.SetTimeZone(nodeConfig.TimeZone);
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
        services.AddScoped<ISchemaProvider, T>();
        services.AddScoped(typeof(T));
        return services;
    }

    /// <summary>
    /// Register the schema storage provider
    /// </summary>
    public static IServiceCollection AddSchemaStorageProvider<T>(this IServiceCollection services)
        where T : class, ISchemaStorageProvider
    {
        services.AddScoped<ISchemaProvider, T>();
        services.TryAddScoped<ISchemaStorageProvider, T>();
        services.AddScoped(typeof(T));
        return services;
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
        services.AddScoped(typeof(T));
        services.TryAddScoped<IAppSchemaDataProvider, T>();
        if (typeof(ISchemaStorageProvider).IsAssignableFrom(typeof(T)))
            services.TryAdd(new ServiceDescriptor(typeof(ISchemaStorageProvider), typeof(T), ServiceLifetime.Scoped));
        return services;
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
}
