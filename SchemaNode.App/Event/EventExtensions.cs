using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Runtime;
using System.Collections.Concurrent;
using ValueType = SchemaNode.Runtime.ValueType;

// ReSharper disable AccessToModifiedClosure

namespace SchemaNode.Event;

/// <summary>
/// The event extensions
/// </summary>
public static class EventExtensions
{
    #region Utility
    
    record EventDispatcher(ConcurrentDictionary<Type, IEventDispatcher> Dispatchers, ConcurrentDictionary<Type, ValueType> Payloads);
    
    static EventDispatcher GetEventDispatchers(SchemaContext context)
    {
        return context.Runtime is not SchemaRuntime runtime 
            ? throw new Exception("The runtime is not a SchemaRuntime") 
            : runtime.GetOrAddRuntimeItem(() => new EventDispatcher(new ConcurrentDictionary<Type, IEventDispatcher>(), new ConcurrentDictionary<Type, ValueType>()));
    }

    // Gets the event dispatcher by type
    static IEventDispatcher? GetEventDispatcher(SchemaContext context, Type type)
    {
        ConcurrentDictionary<Type, IEventDispatcher> dispatchers = GetEventDispatchers(context).Dispatchers;
        
        if (dispatchers.TryGetValue(type, out var dispatcher))
            return dispatcher;

        // Try to get from service provider
        Type dispatchType = typeof(IEventDispatcher<>).MakeGenericType(type);
        dispatcher = context.GetService(dispatchType) as IEventDispatcher;
        if (dispatcher != null)
        {
            dispatchers[type] = dispatcher;
            return dispatcher;
        }

        // Try to get from the super class
        Type? baseType = type.BaseType;
        if (baseType != null && baseType != typeof(object))
        {
            dispatcher = GetEventDispatcher(context, baseType);
            if (dispatcher != null)
            {
                dispatchers[type] = dispatcher;
                return dispatcher;
            }
        }

        return null;
    }

    static ValueType? GetPayloadType(SchemaContext context, Type payloadType)
    {
        ConcurrentDictionary<Type, ValueType> payloads = GetEventDispatchers(context).Payloads;
        
        // Convert the payload to any schema node
        if (!payloads.TryGetValue(payloadType, out ValueType? eventPayload))
        {
            string? schemaType = (context.Runtime as SchemaRuntime)?.GetTypeSchema(payloadType);
            eventPayload = !string.IsNullOrEmpty(schemaType) ? context.GetNodeTypeAsync<ValueType>(schemaType).GetAwaiter().GetResult() : null;
            if (eventPayload != null)
                payloads[payloadType] = eventPayload;
        }
        
        return eventPayload;
    }

    #endregion

    #region Raise Event

    /// <summary>
    /// Raise the event without payload
    /// </summary>
    public static void RaiseEvent<TE>(this SchemaContext context, TE @event) where TE : Event => GetEventDispatcher(context, @event.GetType())?.DispatchEvent(@event);

    /// <summary>
    /// Raise the event
    /// </summary>
    public static void RaiseEvent<TE, TP>(this SchemaContext context, TE @event, TP payLoad) where TE : Event, IEventPayload<TP> where TP : notnull
    {
        @event.Payload = GetPayloadType(context, typeof(TP))?.From(payLoad);

        // Dispatch the event
        GetEventDispatcher(context, @event.GetType())?.DispatchEvent(@event);
    }
    
    /// <summary>
    /// Raise the event
    /// </summary>
    public static void RaiseEvent<TE>(this SchemaContext context, TE @event, DataNode payLoad) where TE : Event
    {
        // Convert the payload to any schema node
        @event.Payload = payLoad;

        // Dispatch the event
        GetEventDispatcher(context, @event.GetType())?.DispatchEvent(@event);
    }
    
    /// <summary>
    /// Raise the event without constructor parameters
    /// </summary>
    public static void RaiseEvent<TE>(this SchemaContext context) where TE : Event, new() => context.RaiseEvent(new TE());

    /// <summary>
    /// Raise the event without constructor parameters
    /// </summary>
    public static void RaiseEvent<TE, TP>(this SchemaContext context, TP payLoad) where TE : Event, IEventPayload<TP>, new() => context.RaiseEvent(new TE(), payLoad);

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
        Type type = eventType.GetCsharpType() ?? throw new Exception($"The CSharp type of {eventType.Name} is not defined");

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