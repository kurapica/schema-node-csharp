using System.Collections.Concurrent;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
// ReSharper disable AccessToModifiedClosure
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable UnusedTypeParameter

namespace SchemaNode.Components;

/// <summary>
/// The base event
/// </summary>
public abstract class Event
{
    /// <summary>
    /// The event identifier
    /// </summary>
    public Guid Id { get; } = Guid.CreateVersion7();

    /// <summary>
    /// The event timestamp
    /// </summary>
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// The event topic name like server/topic/action/guid
    /// So they can be subscribed by wildcard topic, + for one, *,# for multi
    /// </summary>
    public virtual string Topic => string.Empty;

    /// <summary>
    /// The generic payload data
    /// </summary>
    public AnySchemaNode? Payload { get; set; }

    /// <summary>
    /// Match the topic with wildcard support
    /// </summary>
    public bool MatchTopic(string topic)
    {
        if (string.IsNullOrEmpty(Topic) || Topic == "*") return true; // all match
        if (string.IsNullOrEmpty(topic)) return false;

        if (Topic.Equals(topic, StringComparison.OrdinalIgnoreCase)) return true;

        // match with wildcard
        string[] topicParts = Topic.Split(TOPIC_SEP, StringSplitOptions.RemoveEmptyEntries);
        string[] matchParts = topic.Split(TOPIC_SEP, StringSplitOptions.RemoveEmptyEntries);
        if (matchParts.Length > topicParts.Length) return false;

        for (int i = 0; i < matchParts.Length; i++)
        {
            if (matchParts[i] == TOPIC_WILDCARD_SINGLE) continue; // match single part
            if (matchParts[i] == TOPIC_WILDCARD_MULTI || matchParts[i] == TOPIC_WILDCARD_ALL) return true; // match all remaining parts

            if (!topicParts[i].Equals(matchParts[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    } 
}

/// <summary>
/// The event has generic payload, determined by usage
/// </summary>
public interface IEventPayload
{
}

/// <summary>
/// The event with given type payload
/// </summary>
public interface IEventPayload<T>
{
}

/// <summary>
/// The default event dispatcher
/// </summary>
public interface IEventDispatcher
{
    #region Abstract

    /// <summary>
    /// Dispatch the event
    /// </summary>
    void DispatchEvent<TE>(TE @event) where TE: Event;
    
    /// <summary>
    /// Subscribe an event
    /// </summary>
    IDisposable SubscribeEvent<TE>(Type eventType, Action<TE> onEvent) where TE: Event;

    /// <summary>
    /// Subscribe an event by topic
    /// </summary>
    IDisposable SubscribeTopicEvent<TE>(Type eventType, string topic, Action<TE> onEvent) where TE : Event;

    #endregion

    #region Method

    /// <summary>
    /// Subscribe an event
    /// </summary>
    public IDisposable SubscribeEvent<TE>(Action<TE> onEvent) where TE: Event 
        => SubscribeEvent(typeof(TE), onEvent);

    /// <summary>
    /// Subscribe an event by topic
    /// </summary>
    public IDisposable SubscribeTopicEvent<TE>(string topic, Action<TE> onEvent) where TE: Event 
        => SubscribeTopicEvent(typeof(TE), topic, onEvent);

    /// <summary>
    /// Subscribe an event once
    /// </summary>
    public IDisposable SubscribeEventOnce<TE>(Type eventType, Action<TE> onEvent) where TE: Event
    {
        IDisposable? subscription = null;

        subscription = SubscribeEvent(eventType,  (Action<TE>)Handler);
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
    public IDisposable SubscribeEventOnce<TE>(Action<TE> onEvent) where TE: Event => SubscribeEventOnce(typeof(TE), onEvent);

    /// <summary>
    /// Subscribe an event once
    /// </summary>
    public IDisposable SubscribeTopicEventOnce<TE>(Type eventType, string topic, Action<TE> onEvent) where TE: Event
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
    public IDisposable SubscribeTopicEventOnce<TE>(string topic, Action<TE> onEvent) where TE: Event
        => SubscribeTopicEventOnce(typeof(TE), topic, onEvent);

    #endregion
}

/// <summary>
/// The event dispatcher
/// </summary>
public interface IEventDispatcher<in T>: IEventDispatcher where T : Event
{
}

/// <summary>
/// The default event dispatcher
/// </summary>
public class DefaultEventDispatcher : IEventDispatcher<Event>
{
    /// <summary>
    /// Dispatch the event
    /// </summary>
    public void DispatchEvent<TE>(TE @event) where TE : Event
    {
        string? root = !string.IsNullOrEmpty(@event.Topic) ? @event.Topic.Split('/')[0] : null;

        // topic subjects
        if (!string.IsNullOrEmpty(root)
            && TopicEventSubjects.TryGetValue(@event.GetType(), out var topicSubjects) 
            && topicSubjects.TryGetValue(root, out var subject))
        {
            Task.Run(async () =>
            {
                await Task.Yield();
                subject.OnNext(@event);
            });
        }

        // global subjects
        if (GlobalEventSubjects.TryGetValue(@event.GetType(), out var globalSubject))
        {
            Task.Run(async () =>
            {
                await Task.Yield();
                globalSubject.OnNext(@event);
            });
        }
    }

    /// <summary>
    /// Subscribe to the application event
    /// </summary>
    public IDisposable SubscribeEvent<TE>(Type eventType, Action<TE> onEvent) where TE : Event
    {
        var subject = GlobalEventSubjects.GetOrAdd(eventType, _ => new Subject<Event>());
        return subject.SubscribeOn(Scheduler.Default).Subscribe(e => onEvent((TE)e));
    }

    /// <summary>
    /// Subscribe topic event
    /// </summary>
    public IDisposable SubscribeTopicEvent<TE>(Type eventType, string topic, Action<TE> onEvent) where TE : Event
    {
        string? root = !string.IsNullOrEmpty(topic) ? topic.Split('/')[0] : null;
        
        // global subscribe
        if (string.IsNullOrEmpty(root) || root == "*" || root == "#") 
            return SubscribeEvent(eventType, onEvent);
        
        var topicSubjects = TopicEventSubjects.GetOrAdd(eventType, _ => new ConcurrentDictionary<string, Subject<Event>>());
        var subject = topicSubjects.GetOrAdd(root, _ => new Subject<Event>());
        return subject.SubscribeOn(Scheduler.Default).Subscribe(e => onEvent((TE)e));
    }

    static readonly ConcurrentDictionary<Type, Subject<Event>> GlobalEventSubjects = new();
    static readonly ConcurrentDictionary<Type, ConcurrentDictionary<string, Subject<Event>>> TopicEventSubjects = [];

}

/// <summary>
/// The event extensions
/// </summary>
public static class EventExtensions
{
    #region Utility
    
    static readonly ConcurrentDictionary<Type, IEventDispatcher> EventDispatchers = [];
    static readonly ConcurrentDictionary<Type, AnySchemeType> EventPayloads = [];

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
            if (payLoad is AnySchemaNode node)
            {
                @event.Payload = node;
            }
            else
            {
                Type payLoadType = payLoad.GetType();
                if (!EventPayloads.TryGetValue(payLoadType, out AnySchemeType? eventPayload))
                {
                    string? schemaType = payLoad.GetType().GetSchemaType(true);
                    eventPayload = !string.IsNullOrEmpty(schemaType) ? context.GetSchemaTypeAsync(schemaType).GetAwaiter().GetResult() : null;
                    if (eventPayload != null)
                    {
                        EventPayloads[payLoadType] = eventPayload;
                    }
                }
                @event.Payload = eventPayload?.CreateNode(payLoad);
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
                context.Logger.LogError("SubscribeTopicEvent error: {Exception}", ex);
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
                context.Logger.LogError("SubscribeEventOnce error: {Exception}", ex);
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
                context.Logger.LogError("SubscribeEventOnce error: {Exception}", ex);
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
        Type type = eventType.ToCSharpType();
        return GetEventDispatcher(context, type)?.SubscribeEvent(type, onEvent);
    }

    /// <summary>
    /// Subscribe an event by topic
    /// </summary>
    public static IDisposable? SubscribeTopicEvent<TE>(this SchemaContext context, EventType eventType, string topic, Action<TE> onEvent) where TE : Event
    {
        Type type = eventType.ToCSharpType();
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
                context.Logger.LogError("SubscribeTopicEvent error: {Exception}", ex);
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
                context.Logger.LogError("SubscribeEventOnce error: {Exception}", ex);
            }
        }
    }


    /// <summary>
    /// Subscribe an event once
    /// </summary>
    public static IDisposable? SubscribeTopicEventOnce<TE>(this SchemaContext context, EventType eventType, string topic, Action<TE> onEvent) where TE : Event
    {
        Type type = eventType.ToCSharpType();
        
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
                context.Logger.LogError("SubscribeEventOnce error: {Exception}", ex);
            }
        }
    }

    #endregion
}