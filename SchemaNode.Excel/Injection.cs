
using Microsoft.Extensions.DependencyInjection;

namespace SchemaNode.Excel;

public static class Injection
{
    /// <summary>
    /// Add Schema Excel Template Services
    /// </summary>
    public static IServiceCollection AddSchemaExcelTemplate(this IServiceCollection services)
    {
        services.AddSchemaAssemblies(typeof(Injection).Assembly);
        return services;
    }
}