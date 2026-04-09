using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace SchemaNode.Service;

public static class SchemaNodeExtensions
{
    /// <summary>
    /// Add schemas from assemblies
    /// </summary>
    public static IServiceCollection AddSchemaAssemblys(this IServiceCollection services, params Assembly[] assemblies)
    {
        Assembly core = typeof(SchemaNodeExtensions).Assembly;
        Assembly? entry = Assembly.GetEntryAssembly();

        core.ScanSchemaFromAssembly();
        foreach (var assembly in assemblies)
        {
            if (assembly == core) continue;
            if (assembly == entry) entry = null;
            assembly.ScanSchemaFromAssembly();
        }
        entry?.ScanSchemaFromAssembly();
        return services;
    }

    static void ScanSchemaFromAssembly(this Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {

        }
    }
}