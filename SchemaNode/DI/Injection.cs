using System.Reflection;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Provider;
using SchemaNode.Schema;
using static SchemaNode.Utility.Schema;

namespace SchemaNode;

public static class Injection
{
    /// <summary>
    /// Use the schema context with config
    /// </summary>
    public static IServiceCollection AddSchemaContext(this IServiceCollection services, Action<SchemaContextConfig> config)
    {
        config.Invoke(SchemaContext.Config);
        
        // default logger
        services.TryAddSingleton<ILoggerFactory, LoggerFactory>();
        services.TryAddTransient(typeof(ILogger<>), typeof(Logger<>));

        // The schema context
        services.AddTransient<SchemaContext>();
        
        // system.schema types
        return services.AddSchemaSystemTypes<SchemaContext>();
    }
    
    /// <summary>
    /// Register the schema provider
    /// </summary>
    public static IServiceCollection AddSchemaProvider<T>(this IServiceCollection services) where T : class, ISchemaProvider
    {
        services.AddSingleton<ISchemaProvider, T>();
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
    public static IServiceCollection AddSchemaSystemTypes(this IServiceCollection services, Assembly assembly)
    {
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
    /// Enable the schema apis
    /// </summary>
    public static WebApplication UseSchemaApis(this WebApplication app, Action<SchemaApiConfig> config)
    {
        config.Invoke(SchemaContext.ApiConfig);
        _ = Task.Run(() => app.Services.GetRequiredService<SchemaContext>().GetSchemaNodeAsync("", preload: true));
        return app;
    }
}
