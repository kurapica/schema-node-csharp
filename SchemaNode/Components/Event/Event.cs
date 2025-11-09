using Microsoft.Extensions.Logging;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Utility;
using System.Collections.Concurrent;
using static SchemaNode.Utility.Constant;

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
    /// The topic name
    /// </summary>
    public virtual string Topic => string.Empty;

    /// <summary>
    /// The generic payload data
    /// </summary>
    public AnySchemaNode? Payload { get; set; }

    /// <summary>
    /// Match the topic with wildcard support
    /// </summary>
    internal bool MatchTopic(string topic)
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
/// The event with given type payload
/// </summary>
public interface IEventPayload<T>
{
}

public interface IEventDispatcher<T> where T : Event
{
    /// <summary>
    /// Dispatch the event
    /// </summary>
    void DispatchEvent<E>(E @event) where E : T;

    /// <summary>
    /// Subscribe an event
    /// </summary>
    IDisposable SubscribeEvent<E>(Action<E> onEvent) where E: T;

    /// <summary>
    /// Subscribe an event by topic
    /// </summary>
    public IDisposable SubscribeTopicEvent<E>(string topic, Action<E> onEvent) where E: T
    {
        Action<E> handler = (E @event) =>
        {
            try
            {
                if (!@event.MatchTopic(topic)) return;
                onEvent(@event);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SubscribeEventOnce error: {ex}");
            }
        };
        return SubscribeEvent<E>(handler);
    }

    /// <summary>
    /// Subscribe an event once
    /// </summary>
    public IDisposable SubscribeEventOnce<E>(Action<E> onEvent) where E : T
    {
        IDisposable? subscription = null;
        Action<E> handler = (E @event) =>
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
        };

        subscription = SubscribeEvent<E>(handler);
        return subscription;
    }

    /// <summary>
    /// Subscribe an event once
    /// </summary>
    public IDisposable SubscribeTopicEventOnce<E>(string topic, Action<E> onEvent) where E : T
    {
        IDisposable? subscription = null;
        Action<E> handler = (E @event) =>
        {
            try
            {
                if (!@event.MatchTopic(topic)) return;
                subscription?.Dispose();
                onEvent(@event);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SubscribeEventOnce error: {ex}");
            }
        };

        subscription = SubscribeEvent<E>(handler);
        return subscription;
    }
}


public static class EventExtensions
{
    /// <summary>
    /// Raise the event
    /// </summary>
    public static void RaiseEvent(this SchemaContext context, Event @event, object? payLoad = null)
    {
        if (payLoad != null)
        {
            if (payLoad is AnySchemaNode node)
            {
                @event.Payload = node;
            }
            else
            {
                string? schemaType = payLoad.GetType().GetSchemaType(true);
                AnySchemeType? type = !string.IsNullOrEmpty(schemaType) ? context.GetSchemaTypeAsync(schemaType).GetAwaiter().GetResult() : null;
                @event.Payload = type?.CreateNode(payLoad);
            }
        }

        switch (@event)
        {
            case ApplicationEvent appEvent:
                context.GetService<IApplicationEventDispatcher>()?.DispatchEvent(appEvent);
                break;
            case WorkflowEvent wfEvent:
                context.GetService<IWorkflowEventDispatcher>()?.DispatchEvent(wfEvent);
                break;
            case ServerEvent serEvent:
                context.GetService<IServerEventDispatcher>()?.DispatchEvent(serEvent);
                break;
            case ClusterEvent cEvent:
                context.GetService<IClusterEventDispatcher>()?.DispatchEvent(cEvent);
                break;
        }

    }
    /// <summary>
    /// Raise the event
    /// </summary>
    public static void RaiseEvent<T>(this SchemaContext context, object? payLoad = null) where T : Event, new()
    {
        RaiseEvent(context, new T(), payLoad);
    }

    /// <summary>
    /// Subscribe an event
    /// </summary>
    public static IDisposable? SubscribeEvent<E>(this SchemaContext context, Action<E> onEvent) where E : Event
    {
        Type type = typeof(E);

        if (type.IsSubclassOf(typeof(ApplicationEvent)))
        {
            return context.GetService<IApplicationEventDispatcher>()!.SubscribeEvent((Action<ApplicationEvent>)(object)onEvent);
        }
        else if (type.IsSubclassOf(typeof(WorkflowEvent)))
        {
            return context.GetService<IWorkflowEventDispatcher>()!.SubscribeEvent((Action<WorkflowEvent>)(object)onEvent);
        }
        else if (type.IsSubclassOf(typeof(ServerEvent)))
        {
            return context.GetService<IServerEventDispatcher>()!.SubscribeEvent((Action<ServerEvent>)(object)onEvent);
        }
        else if (type.IsSubclassOf(typeof(ClusterEvent)))
        {
            return context.GetService<IClusterEventDispatcher>()!.SubscribeEvent((Action<ClusterEvent>)(object)onEvent);
        }
        return null;
    }

    /// <summary>
    /// Subscribe an event by topic
    /// </summary>
    public static IDisposable? SubscribeTopicEvent<E>(this SchemaContext context, string topic, Action<E> onEvent) where E : Event
    {
        Action<E> handler = (E @event) =>
        {
            try
            {
                if (!@event.MatchTopic(topic)) return;
                onEvent(@event);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SubscribeEventOnce error: {ex}");
            }
        };
        return SubscribeEvent<E>(context, handler);
    }

    /// <summary>
    /// Subscribe an event once
    /// </summary>
    public static IDisposable? SubscribeEventOnce<E>(this SchemaContext context, Action<E> onEvent) where E : Event
    {
        IDisposable? subscription = null;
        Action<E> handler = (E @event) =>
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
        };

        subscription = SubscribeEvent<E>(context, handler);
        return subscription;
    }

    /// <summary>
    /// Subscribe an event once
    /// </summary>
    public static IDisposable SubscribeTopicEventOnce<E>(this SchemaContext context, string topic, Action<E> onEvent) where E : Event
    {
        IDisposable? subscription = null;
        Action<E> handler = (E @event) =>
        {
            try
            {
                if (!@event.MatchTopic(topic)) return;
                subscription?.Dispose();
                onEvent(@event);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SubscribeEventOnce error: {ex}");
            }
        };

        subscription = SubscribeEvent<E>(context, handler);
        return subscription;
    }
}