using System.Collections.Concurrent;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Runtime;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Components;

/// <summary>
/// The application scope event
/// </summary>
public abstract class ApplicationEvent: Event
{
    /// <summary>
    /// The application target
    /// </summary>
    public string Target { get; set; } = string.Empty;
    
    /// <summary>
    /// The application field
    /// </summary>
    public string? Field { get; set; }
}

public static class ApplicationEventExtensions
{
    /// <summary>
    /// Raise the application event
    /// </summary>
    public static void RaiseAppEvent<T>(this SchemaContext context, AppType app, T @event) where T : ApplicationEvent
    {
        var appEventSubjects = AppEventSubjects.GetOrAdd(app.Name, _ => new ConcurrentDictionary<Type, Subject<ApplicationEvent>>());
        var subject = appEventSubjects.GetOrAdd(typeof(T), _ => new Subject<ApplicationEvent>());
        Task.Run(async () =>
        {
            await Task.Yield();
            subject.OnNext(@event);
        });
    }
    
    /// <summary>
    /// Raise the application event
    /// </summary>
    public static void RaiseAppEvent<T>(this SchemaContext context, AppFieldType field, string target) where T: ApplicationEvent
    {
        T @event = Activator.CreateInstance<T>();
        @event.Target = target;
        @event.Field = field.Name;
        context.RaiseAppEvent(field.Application, @event);
    }

    /// <summary>
    /// Raise event with payload
    /// </summary>
    public static void RaiseAppEvent<T>(this SchemaContext context, AppFieldType field, string target, AnySchemaNode payload,
        AnySchemaNode? origin = null) where T : ApplicationEvent, IEventPayload
    {
        T @event = Activator.CreateInstance<T>();
        @event.Target = target;
        @event.Field = field.Name;
        @event.Payload = payload;
        @event.Origin = origin;
        context.RaiseAppEvent(field.Application, @event);
    }

    /// <summary>
    /// Subscribe to the application event
    /// </summary>
    public static IDisposable SubscribeApplicationEvent<T>(this SchemaContext context, AppType app, Action<ApplicationEvent> handler) where T : ApplicationEvent
    {
        var appEventSubjects = AppEventSubjects.GetOrAdd(app.Name, _ => new ConcurrentDictionary<Type, Subject<ApplicationEvent>>());
        var subject = appEventSubjects.GetOrAdd(typeof(T), _ => new Subject<ApplicationEvent>());
        return subject.SubscribeOn(Scheduler.Default).Subscribe(handler);
    }
    
    /// <summary>
    /// Subscribe to the application event
    /// </summary>
    public static IDisposable SubscribeApplicationEvent(this SchemaContext context, Type appEventType, AppType app, Action<ApplicationEvent> handler) 
    {
        var appEventSubjects = AppEventSubjects.GetOrAdd(app.Name, _ => new ConcurrentDictionary<Type, Subject<ApplicationEvent>>());
        var subject = appEventSubjects.GetOrAdd(appEventType, _ => new Subject<ApplicationEvent>());
        return subject.SubscribeOn(Scheduler.Default).Subscribe(handler);
    }

    
    static readonly ConcurrentDictionary<string, ConcurrentDictionary<Type, Subject<ApplicationEvent>>> AppEventSubjects = new();
}