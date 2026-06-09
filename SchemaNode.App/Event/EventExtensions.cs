using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Runtime;
using System.Collections.Concurrent;
// ReSharper disable AccessToModifiedClosure

namespace SchemaNode.Event;

/// <summary>
/// The event extensions
/// </summary>
public static class EventExtensions
{
    #region Utility

    static readonly ConcurrentDictionary<Type, IEventDispatcher> EventDispatchers = [];
    static readonly ConcurrentDictionary<Type, Runtime.ValueType> EventPayloads = [];

    // Gets the event dispatcher by type
    static IEventDispatcher? GetEventDispatcher(SchemaContext context, Type type)
    {
        if (EventDispatchers.TryGetValue(type, out var dispatcher))
            return dispatcher;

        // Try to get from service provider
        Type dispatchType = typeof(IEventDispatcher<>).MakeGenericType(type);
        dispatcher = context.GetService(dispatchType) as IEventDispatcher;
        if (dispatcher != null)
        {
            EventDispatchers[type] = dispatcher;
            return dispatcher;
        }

        // Try to get from the super class
        Type? baseType = type.BaseType;
        if (baseType != null && baseType != typeof(object))
        {
            dispatcher = GetEventDispatcher(context, baseType);
            if (dispatcher != null)
            {
                EventDispatchers[type] = dispatcher;
                return dispatcher;
            }
        }

        return null;
    }

    #endregion

    #region Raise Event

    /// <summary>
    /// Raise the event
    /// </summary>
    public static void RaiseEvent<TE>(this SchemaContext context, TE @event, object? payLoad = null) where TE : Event
    {
        // Convert the payload to any schema node
        if (payLoad != null)
        {
            if (payLoad is DataNode node)
            {
                @event.Payload = node;
            }
            else
            {
                Type payLoadType = payLoad.GetType();
                if (!EventPayloads.TryGetValue(payLoadType, out Runtime.ValueType? eventPayload))
                {
                    string? schemaType = (context.Runtime as SchemaRuntime)?.GetTypeSchema(payLoad.GetType());
                    eventPayload = !string.IsNullOrEmpty(schemaType) ? context.GetNodeTypeAsync<Runtime.ValueType>(schemaType).GetAwaiter().GetResult() : null;
                    if (eventPayload != null)
                    {
                        EventPayloads[payLoadType] = eventPayload;
                    }
                }
                @event.Payload = eventPayload?.From(payLoad);
            }
        }

        // Dispatch the event
        GetEventDispatcher(context, @event.GetType())?.DispatchEvent(@event);
    }

    /// <summary>
    /// Raise the event without constructor parameters
    /// </summary>
    public static void RaiseEvent<TE>(this SchemaContext context, object? payLoad = null) where TE : Event, new()
    {
        RaiseEvent(context, new TE(), payLoad);
    }

    #endregion

    #region Subscribe event by handler

    /// <summary>
    /// Subscribe an event
    /// </summary>
    public static IDisposable? SubscribeEvent<TE>(this SchemaContext context, Action<TE> onEvent) where TE : Event
    {
        return GetEventDispatcher(context, typeof(TE))?.SubscribeEvent(onEvent);
    }

    /// <summary>
    /// Subscribe an event by topic
    /// </summary>
    public static IDisposable? SubscribeTopicEvent<TE>(this SchemaContext context, string topic, Action<TE> onEvent) where TE : Event
    {
        return GetEventDispatcher(context, typeof(TE))?.SubscribeTopicEvent(topic, (Action<TE>)Handler);

        void Handler(TE @event)
        {
            try
            {
                if (!@event.MatchTopic(topic)) return;
                onEvent(@event);
            }
            catch (Exception ex)
            {
                context.LogError("SubscribeTopicEvent error: {Exception}", ex);
            }
        }
    }

    /// <summary>
    /// Subscribe an event once
    /// </summary>
    public static IDisposable? SubscribeEventOnce<TE>(this SchemaContext context, Action<TE> onEvent) where TE : Event
    {
        IDisposable? subscription = null;

        subscription = SubscribeEvent<TE>(context, Handler);
        return subscription;

        void Handler(TE @event)
        {
            try
            {
                if (subscription == null) return;
                subscription?.Dispose();
                subscription = null;
                onEvent(@event);
            }
            catch (Exception ex)
            {
                context.LogError("SubscribeEventOnce error: {Exception}", ex);
            }
        }
    }

    /// <summary>
    /// Subscribe an event once
    /// </summary>
    public static IDisposable? SubscribeTopicEventOnce<TE>(this SchemaContext context, string topic, Action<TE> onEvent) where TE : Event
    {
        IDisposable? subscription = null;
        subscription = GetEventDispatcher(context, typeof(TE))?.SubscribeTopicEvent(topic, (Action<TE>)Handler);
        return subscription;

        void Handler(TE @event)
        {
            try
            {
                if (subscription == null) return;
                if (!@event.MatchTopic(topic)) return;
                subscription?.Dispose();
                subscription = null;
                onEvent(@event);
            }
            catch (Exception ex)
            {
                context.LogError("SubscribeEventOnce error: {Exception}", ex);
            }
        }
    }

    #endregion

    #region Subscribe Event with given schema type

    /// <summary>
    /// Subscribe an event
    /// </summary>
    public static IDisposable? SubscribeEvent<TE>(this SchemaContext context, EventType eventType, Action<TE> onEvent) where TE : Event
    {
        Type type = eventType.GetCsharpType()!;
        return GetEventDispatcher(context, type)?.SubscribeEvent(type, onEvent);
    }

    /// <summary>
    /// Subscribe an event by topic
    /// </summary>
    public static IDisposable? SubscribeTopicEvent<TE>(this SchemaContext context, EventType eventType, string topic, Action<TE> onEvent) where TE : Event
    {
        Type type = eventType.GetCsharpType()!;
        return GetEventDispatcher(context, type)?.SubscribeTopicEvent(type, topic, (Action<TE>)Handler);

        void Handler(TE @event)
        {
            try
            {
                if (!@event.MatchTopic(topic)) return;
                onEvent(@event);
            }
            catch (Exception ex)
            {
                context.LogError("SubscribeTopicEvent error: {Exception}", ex);
            }
        }
    }

    /// <summary>
    /// Subscribe an event once
    /// </summary>
    public static IDisposable? SubscribeEventOnce<TE>(this SchemaContext context, EventType eventType, Action<TE> onEvent) where TE : Event
    {
        IDisposable? subscription = null;
        subscription = SubscribeEvent<TE>(context, eventType, Handler);
        return subscription;

        void Handler(TE @event)
        {
            try
            {
                if (subscription == null) return;
                subscription?.Dispose();
                subscription = null;
                onEvent(@event);
            }
            catch (Exception ex)
            {
                context.LogError("SubscribeEventOnce error: {Exception}", ex);
            }
        }
    }


    /// <summary>
    /// Subscribe an event once
    /// </summary>
    public static IDisposable? SubscribeTopicEventOnce<TE>(this SchemaContext context, EventType eventType, string topic, Action<TE> onEvent) where TE : Event
    {
        Type type = eventType.GetCsharpType();

        IDisposable? subscription = null;
        subscription = GetEventDispatcher(context, type)?.SubscribeTopicEvent(type, topic, (Action<TE>)Handler);
        return subscription;

        void Handler(TE @event)
        {
            try
            {
                if (subscription == null) return;
                if (!@event.MatchTopic(topic)) return;
                subscription?.Dispose();
                subscription = null;
                onEvent(@event);
            }
            catch (Exception ex)
            {
                context.LogError("SubscribeEventOnce error: {Exception}", ex);
            }
        }
    }

    #endregion
}