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
    void DispatchEvent<TE>(TE @event) where TE : Event;

    /// <summary>
    /// Subscribe an event
    /// </summary>
    IDisposable SubscribeEvent<TE>(Action<TE> onEvent) where TE : Event;

    /// <summary>
    /// Subscribe an event by topic
    /// </summary>
    IDisposable SubscribeTopicEvent<TE>(string topic, Action<TE> onEvent) where TE : Event;
}

/// <summary>
/// The event dispatcher
/// </summary>
public interface IEventDispatcher<in T> : IEventDispatcher where T : Event;