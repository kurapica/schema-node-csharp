using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Components;

namespace SchemaNode.Kafka;

/// <summary>
/// The Kafka event extension
/// </summary>
public static class KafkaEventExtension
{
    /// <summary>
    /// Add schema kafka event support
    /// </summary>
    public static IServiceCollection AddSchemaKafkaEvent(this IServiceCollection services)
    {
        services.AddSingleton<IEventSource, KafkaEventSource>();
        services.AddSchemaAssemblies(typeof(KafkaEventExtension).Assembly);
        return services;
    }
}