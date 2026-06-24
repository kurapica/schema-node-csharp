using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchemaNode.Context;
using SchemaNode.Data;
using SchemaNode.Data.Sql;
using SchemaNode.Runtime;
using SchemaNode.Schema.Provider;
using SchemaNode.Utility;

namespace SchemaNode.Service;

public static class AppService
{    
    /// <summary>
    /// Register the app schema provider, also the node schema provider
    /// </summary>
    public static IServiceCollection AddAppSchemaProvider<T>(this IServiceCollection services) 
        where T : class, IAppSchemaProvider
        => services.AddScoped<IAppSchemaProvider>(sp => sp.GetRequiredService<T>()).AddSchemaProvider<T>();

    /// <summary>
    /// Register the app schema storage provider, it also will be used for <see cref="AddAppSchemaProvider{T}"/>
    /// </summary>
    public static IServiceCollection AddSchemaStorageProvider<T>(this IServiceCollection services)
        where T : class, IAppSchemaStorageProvider
    {
        services.TryAddScoped<IAppSchemaStorageProvider>(sp => sp.GetRequiredService<T>()); // single per service
        return services.AddAppSchemaProvider<T>();
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
        Type? interfaceType = typeof(T).GetInterfaces().FirstOrDefault(i => i.IsSubclassOfGenericType(typeof(IAppDataSqlProvider<>)));
        if (interfaceType != null)
        {
            // keep it simple, just set it
            ISqlProvider instance = (ISqlProvider)Activator.CreateInstance(interfaceType.GetGenericArguments()[0])!;
            services.AddSingleton(instance);
        }
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

        services.AddSchemaAssembly<AppSchemaRuntime>();
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
}