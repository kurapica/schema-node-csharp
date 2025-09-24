using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Components.Provider;
using SchemaNode.Schema;
using static SchemaNode.Utility.Schema;

namespace SchemaNode;

public static class Injection
{
    /// <summary>
    /// Use the schema context with config
    /// </summary>
    public static IServiceCollection AddSchemaNode(this IServiceCollection services, Action<SchemaContextConfig> config)
    {
        config.Invoke(SchemaContext.Config);
        
        // default logger
        services.TryAddSingleton<ILoggerFactory, LoggerFactory>();
        services.TryAddScoped(typeof(ILogger<>), typeof(Logger<>));
        
        // message handlers
        Components.SchemaMessageHandlerExtensions.RegisterSchemaMessageHandlers<SchemaContext>(services);
        Components.SchemaMessageHandlerExtensions.RegisterSchemaMessageHandlers(services, Assembly.GetEntryAssembly());

        // The schema context
        services.AddScoped<SchemaContext>();
        
        // system.schema types
        services.AddSchemaSystemTypes<SchemaContext>();
        return services.AddSchemaSystemTypes(Assembly.GetEntryAssembly());
    }
    
    /// <summary>
    /// Register the schema provider
    /// </summary>
    public static IServiceCollection AddSchemaProvider<T>(this IServiceCollection services) 
        where T : class, ISchemaProvider
    {
        services.AddScoped<ISchemaProvider, T>();
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
        
        SchemaNameSpaceAttribute? rootNamespaceAttr = assembly.GetCustomAttribute<SchemaNameSpaceAttribute>();
        if (rootNamespaceAttr != null)
        {
            SaveSystemNodeSchema(new NodeSchema
            {
                Name = rootNamespaceAttr.Name,
                Type = SchemaType.Namespace,
                Display = rootNamespaceAttr.Display,
            });
        }

        // scan all
        foreach (var type in assembly.GetTypes())
            type.GetSchemaType();
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
            return await scope.ServiceProvider.GetRequiredService<SchemaContext>()
                .GetSchemaNodeAsync("", preload: true);
        });
        return app;
    }
}
