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
        if (AppEventSubjects.TryGetValue(@event.GetType(), out var subject))
        {
            Task.Run(async () =>
            {
                await Task.Yield();
                subject.OnNext(@event);
            });
        }
    }

    /// <summary>
    /// Subscribe to the application event
    /// </summary>
    public IDisposable SubscribeEvent<E>(Type eventType, Action<E> onEvent) where E : Event
    {
        var subject = AppEventSubjects.GetOrAdd(eventType, _ => new Subject<Event>());
        return subject.SubscribeOn(Scheduler.Default).Subscribe(e => onEvent((E)e));
    }
    
    static readonly ConcurrentDictionary<Type, Subject<Event>> AppEventSubjects = [];
}
