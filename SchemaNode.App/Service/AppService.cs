using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchemaNode.Context;
using SchemaNode.Runtime;

namespace SchemaNode.Service;

public static class AppService
{
    /// <summary>
    /// Add application schema assemblies to service collection
    /// </summary>
    public static IServiceCollection AddAppSchemaAssemblies(this IServiceCollection services, params Assembly[] assemblies)
    {
        // default run-time
        services.TryAddSingleton<ISchemaRuntime, AppSchemaRuntime>();
        
        services.AddSchemaAssemblies(assemblies);
        
        return services;
    }
    
    /// <summary>
    /// Add schema assembly of the given type
    /// </summary>
    public static IServiceCollection AddAppSchemaAssembly<T>(this IServiceCollection services) where T: class 
        => services.AddAppSchemaAssemblies(typeof(T).Assembly);
}