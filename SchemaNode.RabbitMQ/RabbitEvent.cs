using SchemaNode.Components;

namespace SchemaNode.RabbitMQ;

/// <summary>
/// The RabbitMQ Event
/// </summary>
public abstract class RabbitEvent: Event
{
}

public abstract class RabbitEvent<TPayload> : RabbitEvent, IEventPayload<TPayload>
{
}