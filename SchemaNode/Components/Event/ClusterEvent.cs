using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Runtime;

namespace SchemaNode.Components;

public abstract class ClusterEvent<T>: Event<T>
{
    /// <summary>
    /// The event scope
    /// </summary>
    public override EventScope Scope => EventScope.Workflow;
    
    /// <summary>
    /// Raise the cluster event
    /// </summary>
    /// <param name="context"></param>
    public void Raise(SchemaContext context)
    {
        IClusterEventScheduler? handler = context.ServiceProvider.GetService<IClusterEventScheduler>();
        handler?.Schedule(this);
        
        context.Logger.LogInformation("[ClusterEvent]{EventId} [Topic]{Topic} [Payload]{Payload}", Id, Topic, Payload);
    }
}