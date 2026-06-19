using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchemaNode.Context;
using SchemaNode.Data;
using SchemaNode.Http;
using SchemaNode.Runtime;
using SchemaNode.Schema.Provider;

namespace SchemaNode.Service;

public static class AppService
{    
    /// <summary>
    /// Register the app schema provider
    /// </summary>
    public static IServiceCollection AddSchemaProvider<T>(this IServiceCollection services) 
        where T : class, IAppSchemaProvider
    {
        services.TryAddScoped<T>();
        services.AddScoped<IAppSchemaProvider>(sp => sp.GetRequiredService<T>()); // multi allowed
        services.AddScoped<INodeSchemaProvider>(sp => sp.GetRequiredService<T>());
        return services;
    }

    /// <summary>
    /// Register the app schema storage provider, it also will be used for <see cref="AddSchemaProvider"/>
    /// </summary>
    public static IServiceCollection AddSchemaStorageProvider<T>(this IServiceCollection services)
        where T : class, IAppSchemaStorageProvider
    {
        services.TryAddScoped<IAppSchemaStorageProvider>(sp => sp.GetRequiredService<T>()); // single per service
        return services.AddSchemaProvider<T>();
    }

    /// <summary>
    /// Register app data provider
    /// </summary>
    public static IServiceCollection AddAppDataProvider<T>(this IServiceCollection services)
        where T : class, IAppDataProvider
    {
        services.TryAddScoped<T>();
        services.TryAddScoped<IAppDataProvider>(sp => sp.GetRequiredService<T>());
        
        // sql provider check
        /*Type? interfaceType = typeof(T).GetInterfaces().FirstOrDefault(i => i.IsSubclassOfGenericType(typeof(IAppDataSqlProvider<>)));
        if (interfaceType != null)
        {
            // keep it simple, just set it
            ISqlProvider instance = (ISqlProvider)Activator.CreateInstance(interfaceType.GetGenericArguments()[0])!;
            services.AddSingleton(instance);
        }*/
        
        return services;
    }

    /// <summary>
    /// Add application schema assemblies to service collection, use this instead of <see cref="AddAppSchemaAssemblies"/>
    /// </summary>
    public static IServiceCollection AddAppSchemaAssemblies(this IServiceCollection services, params Assembly[] assemblies)
    {
        // default run-time
        services.TryAddSingleton<ISchemaRuntime, AppSchemaRuntime>();
        
        // The schema runtime builder
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IRuntimeStageHandler, AppRuntimeStageHandler>());
        
        services.AddSchemaAssemblies(assemblies);
        
        return services;
    }
    
    /// <summary>
    /// Add schema assembly of the given type
    /// </summary>
    public static IServiceCollection AddAppSchemaAssembly<T>(this IServiceCollection services) where T: class 
        => services.AddAppSchemaAssemblies(typeof(T).Assembly);

    /// <summary>
    /// Set the schema configs
    /// </summary>
    public static IServiceCollection WithSchemaConfig(this IServiceCollection services, Action<SchemaNodeConfig> config)
    {
        var options = new SchemaNodeConfig();
        config.Invoke(options);
        services.AddSingleton(options);
        return services;
    }
    
    /// <summary>
    /// Sets the api protocol
    /// </summary>
    public static IServiceCollection WithSchemaApiProtocol<T>(this IServiceCollection services) where T: class, ISchemaApiProtocol
    {
        services.AddTransient<ISchemaApiProtocol, T>();
        return services;
    }
}