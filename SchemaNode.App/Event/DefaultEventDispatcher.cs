using System.Collections.Concurrent;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace SchemaNode.Event;

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
        string? root = !string.IsNullOrEmpty(@event.Topic) ? @event.Topic.Split(['/', '.'])[0] : null;

        // topic subjects
        if (!string.IsNullOrEmpty(root)
            && _topicEventSubjects.TryGetValue(@event.GetType(), out var topicSubjects)
            && topicSubjects.TryGetValue(root, out var subject))
        {
            Task.Run(async () =>
            {
                await Task.Yield();
                subject.OnNext(@event);
            });
        }

        // global subjects
        if (_globalEventSubjects.TryGetValue(@event.GetType(), out var globalSubject))
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
    public IDisposable SubscribeEvent<TE>(Action<TE> onEvent) where TE : Event
    {
        var subject = _globalEventSubjects.GetOrAdd(typeof(TE), _ => new Subject<Event>());
        return subject.SubscribeOn(Scheduler.Default).Subscribe(e => onEvent((TE)e));
    }

    /// <summary>
    /// Subscribe topic event
    /// </summary>
    public IDisposable SubscribeTopicEvent<TE>(string topic, Action<TE> onEvent) where TE : Event
    {
        Type eventType = typeof(TE);
        string? root = !string.IsNullOrEmpty(topic) ? topic.Split(['/', '.'])[0] : null;

        // global subscribe
        if (string.IsNullOrEmpty(root) || root == "*" || root == "#")
            return SubscribeEvent(onEvent);

        var topicSubjects = _topicEventSubjects.GetOrAdd(eventType, _ => new ConcurrentDictionary<string, Subject<Event>>());
        var subject = topicSubjects.GetOrAdd(root, _ => new Subject<Event>());
        return subject.SubscribeOn(Scheduler.Default).Subscribe(e => onEvent((TE)e));
    }

    private readonly ConcurrentDictionary<Type, Subject<Event>> _globalEventSubjects = new();
    private readonly ConcurrentDictionary<Type, ConcurrentDictionary<string, Subject<Event>>> _topicEventSubjects = [];
}
