namespace SchemaNode.Event;

/// <summary>
/// The event dispatcher
/// </summary>
public interface IEventDispatcher
{
    #region Abstract

    /// <summary>
    /// Dispatch the event
    /// </summary>
    void DispatchEvent<TE>(TE @event) where TE : Event;

    /// <summary>
    /// Subscribe an event
    /// </summary>
    IDisposable SubscribeEvent<TE>(Type eventType, Action<TE> onEvent) where TE : Event;

    /// <summary>
    /// Subscribe an event by topic
    /// </summary>
    IDisposable SubscribeTopicEvent<TE>(Type eventType, string topic, Action<TE> onEvent) where TE : Event;

    #endregion

    #region Method

    /// <summary>
    /// Subscribe an event
    /// </summary>
    public IDisposable SubscribeEvent<TE>(Action<TE> onEvent) where TE : Event
        => SubscribeEvent(typeof(TE), onEvent);

    /// <summary>
    /// Subscribe an event by topic
    /// </summary>
    public IDisposable SubscribeTopicEvent<TE>(string topic, Action<TE> onEvent) where TE : Event
        => SubscribeTopicEvent(typeof(TE), topic, onEvent);

    /// <summary>
    /// Subscribe an event once
    /// </summary>
    public IDisposable SubscribeEventOnce<TE>(Type eventType, Action<TE> onEvent) where TE : Event
    {
        IDisposable? subscription = null;

        subscription = SubscribeEvent(eventType, (Action<TE>)Handler);
        return subscription;

        void Handler(TE @event)
        {
            try
            {
                subscription?.Dispose();
                onEvent(@event);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SubscribeEventOnce error: {ex}");
            }
        }
    }

    /// <summary>
    /// Subscribe an event once
    /// </summary>
    public IDisposable SubscribeEventOnce<TE>(Action<TE> onEvent) where TE : Event => SubscribeEventOnce(typeof(TE), onEvent);

    /// <summary>
    /// Subscribe an event once
    /// </summary>
    public IDisposable SubscribeTopicEventOnce<TE>(Type eventType, string topic, Action<TE> onEvent) where TE : Event
    {
        IDisposable? subscription = null;

        subscription = SubscribeTopicEvent(eventType, topic, (Action<TE>)Handler);
        return subscription;

        void Handler(TE @event)
        {
            try
            {
                subscription?.Dispose();
                onEvent(@event);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SubscribeEventOnce error: {ex}");
            }
        }
    }

    /// <summary>
    /// Subscribe an event once
    /// </summary>
    public IDisposable SubscribeTopicEventOnce<TE>(string topic, Action<TE> onEvent) where TE : Event
        => SubscribeTopicEventOnce(typeof(TE), topic, onEvent);

    #endregion
}

/// <summary>
/// The event dispatcher
/// </summary>
public interface IEventDispatcher<in T> : IEventDispatcher where T : Event
{
}