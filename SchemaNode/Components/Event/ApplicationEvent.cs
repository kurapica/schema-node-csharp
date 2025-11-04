using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchemaNode.Context;
using SchemaNode.Enum;

namespace SchemaNode.Components;

public abstract class ApplicationEvent<T>: Event<T>
{
    /// <summary>
    /// The event scope
    /// </summary>
    public override EventScope Scope => EventScope.Application;
    
    /// <summary>
    /// The application name
    /// </summary>
    public string Application { get; set; } = string.Empty;
    
    /// <summary>
    /// The application target
    /// </summary>
    public string Target { get; set; } = string.Empty;
    
    /// <summary>
    /// The application field
    /// </summary>
    public string? Field { get; set; }
    
    /// <summary>
    /// Raise the application event
    /// </summary>
    public void Raise(SchemaContext context)
    {
        IApplicationEventScheduler? handler = context.ServiceProvider.GetService<IApplicationEventScheduler>();
        handler?.Schedule(this);
        
        context.Logger.LogInformation("[ClusterEvent]{EventId} [Topic]{Topic} [App]{Application} [Target]{Target} [Field]{Field}"
            , Id, Topic, Application, Target, Field);
    }
}