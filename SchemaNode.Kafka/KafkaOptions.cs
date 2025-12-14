namespace SchemaNode.Kafka;

public class KafkaOptions
{
    /// <summary>
    /// The Kafka bootstrap servers
    /// </summary>
    public string BootstrapServers { get; set; } = string.Empty;

    /// <summary>
    /// The Kafka consumer group id
    /// </summary>
    public string GroupId { get; set; } = string.Empty;
}
