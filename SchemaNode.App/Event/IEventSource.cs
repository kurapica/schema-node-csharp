using SchemaNode.Context;

namespace SchemaNode.Event;

/// <summary>
/// External event source (Kafka / MQTT / RabbitMQ)
/// </summary>
public interface IEventSource
{
    /// <summary>
    /// Called when SchemaContext is ready
    /// </summary>
    Task StartAsync(SchemaContext context, CancellationToken token);

    /// <summary>
    /// Stop consuming
    /// </summary>
    Task StopAsync(CancellationToken token);
}
