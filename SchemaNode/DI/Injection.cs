using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SchemaNode.Context;
using SchemaNode.Provider;

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
        return services;
    }
    
    /// <summary>
    /// Register the schema provider
    /// </summary>
    public static IServiceCollection UseSchemaProvider<T>(this IServiceCollection services) where T : class, ISchemaProvider
    {
        services.AddSingleton<ISchemaProvider, T>();
        return services;
    }
}
