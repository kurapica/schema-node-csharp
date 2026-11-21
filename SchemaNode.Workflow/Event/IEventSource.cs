using SchemaNode.Context;

namespace SchemaNode.Event;

/// <summary>
/// External event source (Kafka / MQTT / RabbitMQ)
/// </summary>
public interface IEventSource
{
    /// <summary>
    /// Start the event consuming
    /// </summary>
    Task StartAsync(SchemaContext context, CancellationToken token);

    /// <summary>
    /// Stop consuming
    /// </summary>
    Task StopAsync(CancellationToken token);
}
