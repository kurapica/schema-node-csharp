using System.Collections.Concurrent;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Components;

/// <summary>
/// The application scope event
/// </summary>
public abstract class ApplicationEvent(string app): Event
{
    /// <summary>
    /// The topic
    /// </summary>
    public override string Topic => app.ToLower().Replace(".", "_");
}

/// <summary>
/// The application data event, normally for target app data access
/// </summary>
/// <param name="app"></param>
/// <param name="target"></param>
public abstract class ApplicationDataEvent(string app, string target): ApplicationEvent(app)
{
    /// <summary>
    /// The topic
    /// </summary>
    public override string Topic => $"{base.Topic}/{target}";
}

/// <summary>
/// The application field data event, normally for specific field data update
/// </summary>
/// <param name="app"></param>
/// <param name="target"></param>
/// <param name="field"></param>
public abstract class ApplicationFieldDataEvent(string app, string target, string field): ApplicationDataEvent(app, target)
{
    public override string Topic => $"{base.Topic}/{field}";
}

/// <summary>
/// The application event dispatcher
/// </summary>
public interface IApplicationEventDispatcher: IEventDispatcher<ApplicationEvent>
{
}

public class DefaultApplicationEventDispatcher : IApplicationEventDispatcher
{
    /// <summary>
    /// Dispatch the event
    /// </summary>
    public void DispatchEvent<E>(E @event) where E : ApplicationEvent
    {
        string app = @event.Topic.Split('/')[0];

        // app subjects
        if (AppEventSubjects.TryGetValue(@event.GetType(), out ConcurrentDictionary<string, Subject<Event>>? appSubjects) 
            && appSubjects.TryGetValue(app, out var subject))
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
    public IDisposable SubscribeEvent<E>(Type eventType, Action<E> onEvent) where E : Event
    {
        var subject = GlobalEventSubjects.GetOrAdd(eventType, _ => new Subject<Event>());
        return subject.SubscribeOn(Scheduler.Default).Subscribe(e => onEvent((E)e));
    }

    /// <summary>
    /// Subscribe topic event
    /// </summary>
    public IDisposable SubscribeTopicEvent<E>(Type eventType, string topic, Action<E> onEvent) where E : Event
    {
        string app = topic.Split('/')[0];
        if (string.IsNullOrEmpty(app) || app == "*" || app == "#")
        {
            // global subscribe
            return SubscribeEvent(eventType, onEvent);
        }
        var appSubjects = AppEventSubjects.GetOrAdd(eventType, _ => new ConcurrentDictionary<string, Subject<Event>>());
        var subject = appSubjects.GetOrAdd(app, _ => new Subject<Event>());
        return subject.SubscribeOn(Scheduler.Default).Subscribe(e => onEvent((E)e));
    }

    static readonly ConcurrentDictionary<Type, Subject<Event>> GlobalEventSubjects = new();
    static readonly ConcurrentDictionary<Type, ConcurrentDictionary<string, Subject<Event>>> AppEventSubjects = [];
}
