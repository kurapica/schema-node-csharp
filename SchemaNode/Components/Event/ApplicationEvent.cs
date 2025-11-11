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
    /// The application
    /// </summary>
    public string Application { get; } = app;

    /// <summary>
    /// The topic
    /// </summary>
    public override string Topic => Application.ToLower().Replace(".", "_");
}

public abstract class ApplicationDataEvent(string app, string target): ApplicationEvent(app)
{
    /// <summary>
    /// The target identifier
    /// </summary>
    public string Target { get; } = target;

    /// <summary>
    /// The topic
    /// </summary>
    public override string Topic => $"{base.Topic}/{Target}";
}

public abstract class  ApplicationFieldDataEvent(string app, string target, string field): ApplicationDataEvent(app, target)
{
    /// <summary>
    /// The application field name
    /// </summary>
    public string Field { get; } = field;

    public override string Topic => $"{base.Topic}/{Field}";
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
        // app subjects
        if (AppEventSubjects.TryGetValue(@event.GetType(), out ConcurrentDictionary<string, Subject<Event>>? appSubjects) 
            && appSubjects.TryGetValue(@event.Application.ToLower().Replace(".", "_"), out var subject))
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
    public IDisposable SubscribeTopicEvent<E>(string topic, Action<E> onEvent) where E : Event
    {
        string app = topic.Split('/')[0];
        var appSubjects = AppEventSubjects.GetOrAdd(typeof(E), _ => new ConcurrentDictionary<string, Subject<Event>>());
        var subject = appSubjects.GetOrAdd(app, _ => new Subject<Event>());
        return subject.SubscribeOn(Scheduler.Default).Subscribe(e => onEvent((E)e));
    }

    static readonly ConcurrentDictionary<Type, Subject<Event>> GlobalEventSubjects = new();
    static readonly ConcurrentDictionary<Type, ConcurrentDictionary<string, Subject<Event>>> AppEventSubjects = [];
}
