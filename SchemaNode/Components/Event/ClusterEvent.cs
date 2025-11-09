namespace SchemaNode.Components;

/// <summary>
/// The cluster scope event
/// </summary>
public abstract class ClusterEvent: Event
{
}

/// <summary>
/// The cluster event dispatcher
/// </summary>
public interface IClusterEventDispatcher: IEventDispatcher<ClusterEvent>
{
}