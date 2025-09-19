using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Provider;
using SchemaNode.Schema;
using static SchemaNode.Utility.Schema;

namespace SchemaNode.DI;

public static class Injection
{
    /// <summary>
    /// Use the schema context with config
    /// </summary>
    public static IServiceCollection UseSchemaContext(this IServiceCollection services, Action<SchemaContextConfig> config)
    {
        config.Invoke(SchemaContext.Config);
        
        // default logger
        services.TryAdd(ServiceDescriptor.Singleton<ILoggerFactory, LoggerFactory>());
        services.TryAdd(ServiceDescriptor.Transient(typeof(ILogger<>), typeof(Logger<>)));
        return services.RegisterSystemTypes<SchemaContext>();
    }
    
    /// <summary>
    /// Register the schema provider
    /// </summary>
    public static IServiceCollection UseSchemaProvider<T>(this IServiceCollection services) where T : class, ISchemaProvider
    {
        services.AddSingleton<ISchemaProvider, T>();
        return services;
    }
    
    /// <summary>
    /// Register system types from the assembly that contains the given type
    /// </summary>
    public static IServiceCollection RegisterSystemTypes<T>(this IServiceCollection services)
    {
        return RegisterSystemTypes(services, typeof(T));
    }

    /// <summary>
    /// Register system types from the assembly that contains the given type
    /// </summary>
    public static IServiceCollection RegisterSystemTypes(this IServiceCollection services, Type type)
    {
        return RegisterSystemTypes(services, type.Assembly);
    }
    
    /// <summary>
    /// Register system types from the assembly that contains the given type
    /// </summary>
    public static IServiceCollection RegisterSystemTypes(this IServiceCollection services, Assembly assembly)
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
}
