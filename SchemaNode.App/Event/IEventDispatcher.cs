// ReSharper disable AccessToModifiedClosure
// ReSharper disable UnusedTypeParameter
namespace SchemaNode.Event;

/// <summary>
/// The event dispatcher
/// </summary>
public interface IEventDispatcher
{
    /// <summary>
    /// Dispatch the event
    /// </summary>
    void DispatchEvent<TE>(TE @event) where TE : BaseEvent;

    /// <summary>
    /// Subscribe an event
    /// </summary>
    IDisposable SubscribeEvent<TE>(Type eventType,Action<TE> onEvent) where TE : BaseEvent;

    /// <summary>
    /// Subscribe an event by topic
    /// </summary>
    IDisposable SubscribeTopicEvent<TE>(Type eventType,string topic, Action<TE> onEvent) where TE : BaseEvent;
}

/// <summary>
/// The event dispatcher
/// </summary>
public interface IEventDispatcher<in T> : IEventDispatcher where T : BaseEvent;