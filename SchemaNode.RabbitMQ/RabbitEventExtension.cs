using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Event;
using SchemaNode.Service;

namespace SchemaNode.RabbitMQ;

/// <summary>
/// RabbitMQ event extension
/// </summary>
public static class RabbitEventExtension
{
    public static IServiceCollection AddSchemaRabbitEvent(this IServiceCollection services)
    {
        services.AddSingleton<IEventSource, RabbitEventSource>();
        services.AddSchemaAssembly<RabbitEventSource>();
        return services;
    }
}