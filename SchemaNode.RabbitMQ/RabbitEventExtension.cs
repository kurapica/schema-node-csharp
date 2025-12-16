using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Components;

namespace SchemaNode.RabbitMQ;

/// <summary>
/// RabbitMQ event extension
/// </summary>
public static class RabbitEventExtension
{
    public static IServiceCollection AddSchemaRabbitEvent(this IServiceCollection services)
    {
        services.AddSingleton<IEventSource, RabbitEventSource>();
        services.AddSchemaAssemblies(typeof(RabbitEventExtension).Assembly);
        return services;
    }
}