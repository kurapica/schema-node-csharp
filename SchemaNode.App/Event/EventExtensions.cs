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
    
    record EventRuntimeInfo(ConcurrentDictionary<Type, IEventDispatcher> Dispatchers);
    
    static EventRuntimeInfo GetEventRuntimeInfo(SchemaContext context)
        => context.Runtime is SchemaRuntime runtime 
            ? runtime.GetOrAddRuntimeItem(() => new EventRuntimeInfo([]))
            : throw new Exception("The runtime is not a SchemaRuntime");
    
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

    extension(SchemaContext context)
    {
        #region Raise Event

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
        public void RaiseEvent<TE, TP>(TP payLoad) where TE : Event, IEventPayload<TP>, new() where TP : notnull 
            => context.RaiseEvent(new TE(), payLoad);
        
        /// <summary>
        /// Raise the event without constructor parameters
        /// </summary>
        public void RaiseEvent<TE>(DataNode payLoad) where TE : Event, new()
            => context.RaiseEvent(new TE(), payLoad);
        
        #endregion
        
        #region Subscribe event by handler

        /// <summary>
        /// Subscribe an event
        /// </summary>
        public IDisposable? SubscribeEvent<TE>(Action<TE> onEvent) where TE : Event
        {
            return GetEventDispatcher(context, typeof(TE))?.SubscribeEvent(onEvent);
        }

        /// <summary>
        /// Subscribe an event by topic
        /// </summary>
        public IDisposable? SubscribeTopicEvent<TE>(string topic, Action<TE> onEvent) where TE : Event
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
        public IDisposable? SubscribeEventOnce<TE>(Action<TE> onEvent) where TE : Event
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
        public IDisposable? SubscribeTopicEventOnce<TE>(string topic, Action<TE> onEvent) where TE : Event
        {
            IDisposable? subscription = null;
            subscription = context.SubscribeTopicEvent(topic, (Action<TE>)Handler);
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

}