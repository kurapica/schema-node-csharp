using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchemaNode.Context;
using SchemaNode.Enum;

namespace SchemaNode.Components;

public abstract class ServerEvent<T>: Event<T>
{
    /// <summary>
    /// The event scope
    /// </summary>
    public override EventScope Scope => EventScope.Server;
    
    /// <summary>
    /// Raise the server event
    /// </summary>
    public void Raise(SchemaContext context)
    {
        IServerEventScheduler? handler = context.ServiceProvider.GetService<IServerEventScheduler>();
        handler?.Schedule(this);
        
        context.Logger.LogInformation("[ServerEvent]{EventId} [Topic]{Topic} [Payload]{Payload}", Id, Topic, Payload);
    }
}