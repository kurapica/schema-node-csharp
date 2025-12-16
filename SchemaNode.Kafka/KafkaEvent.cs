using SchemaNode.Components;

namespace SchemaNode.Kafka;

/// <summary>
/// THe Kafka Event
/// </summary>
public abstract class KafkaEvent: Event
{
}

/// <summary>
/// Kafka event with typed payload
/// </summary>
public abstract class KafkaEvent<TPayload> : KafkaEvent, IEventPayload<TPayload>
{
}