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
    
    record EventRuntimeInfo(
        ConcurrentDictionary<Type, IEventDispatcher> Dispatchers, 
        ConcurrentDictionary<Type, EventType> EventTypes);
    
    static EventRuntimeInfo GetEventRuntimeInfo(SchemaContext context)
    {
        return context.Runtime is not SchemaRuntime runtime 
            ? throw new Exception("The runtime is not a SchemaRuntime") 
            : runtime.GetOrAddRuntimeItem(() => new EventRuntimeInfo([],[]));
    }
    
    // Gets the event dispatcher by type
    static IEventDispatcher? GetEventDispatcher(SchemaContext context, Type? type)
    {
        if (type == null || type == typeof(object)) return null;
        EventRuntimeInfo runtimeInfo = GetEventRuntimeInfo(context);
        
        if  (runtimeInfo.Dispatchers.TryGetValue(type, out IEventDispatcher? dispatcher)) return dispatcher;

        dispatcher = context.GetService(typeof(IEventDispatcher<>).MakeGenericType(type)) as IEventDispatcher
                     ?? GetEventDispatcher(context, type.BaseType)
                     ?? throw new Exception($"No dispatcher for event type {type.FullName}");
        runtimeInfo.Dispatchers[type] = dispatcher;
        return dispatcher;
    }

    #endregion

    #region Raise Event

    extension(SchemaContext context)
    {
        /// <summary>
        /// Raise the event without payload
        /// </summary>
        public void RaiseEvent<TE>(TE @event) where TE : Event 
            => GetEventDispatcher(context, @event.GetType())?.DispatchEvent(@event);

        /// <summary>
        /// Raise the event with payload, calc the 
        /// </summary>
        public void RaiseEvent<TE, TP>(TE @event, TP payLoad) where TE : Event, IEventPayload<TP> where TP : notnull
        {
            @event.Payload = context.GetSchemaNodeAsync(payLoad).GetAwaiter().GetResult();
            context.RaiseEvent(@event);
        }

        /// <summary>
        /// Raise the event with data node, no check for the payload type
        /// </summary>
        public void RaiseEvent<TE>(TE @event, DataNode payLoad) where TE : Event
        {
            // Use directly
            @event.Payload = payLoad;
            context.RaiseEvent(@event);
        }

        /// <summary>
        /// Raise the event without constructor parameters
        /// </summary>
        public void RaiseEvent<TE>() where TE : Event, new() => context.RaiseEvent(new TE());

        /// <summary>
        /// Raise the event without constructor parameters
        /// </summary>
        public void RaiseEvent<TE, TP>(TP payLoad) where TE : Event, IEventPayload<TP>, new() where TP : notnull => context.RaiseEvent(new TE(), payLoad);
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
                if (!@event.IsTopicMatch(topic)) return;
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
                if (!@event.IsTopicMatch(topic)) return;
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
                if (!@event.IsTopicMatch(topic)) return;
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
                if (!@event.IsTopicMatch(topic)) return;
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