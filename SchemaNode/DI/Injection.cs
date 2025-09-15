using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        return services;
    }
    
    /// <summary>
    /// Use the schema context with config
    /// </summary>
    public static IServiceCollection UseSchemaContext(this IServiceCollection services, SchemaContextConfig config)
    {
        SchemaContext.Config = config;
        return services;
    }

    /// <summary>
    /// Register the schema provider
    /// </summary>
    public static IServiceCollection UseSchemaProvider<T>(this IServiceCollection services) where T : class, ISchemaProvider
    {
        services.TryAdd(ServiceDescriptor.Singleton<ISchemaProvider, T>());
        return services;
    }
}