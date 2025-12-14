namespace SchemaNode.Kafka;

/// <summary>
/// The kafka event topic attribute
/// </summary>
/// <param name="topic"></param>

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class KafkaTopicAttribute(string topic): System.Attribute
{
    /// <summary>
    /// The Kafka topic name
    /// </summary>
    public string Topic { get; } = topic;
}