using SchemaNode.Event;

namespace SchemaNode.RabbitMQ;

/// <summary>
/// The RabbitMQ Event
/// </summary>
public abstract class RabbitEvent : BaseEvent;

public abstract class RabbitEvent<TPayload> : RabbitEvent, IEventPayload<TPayload>;