namespace SchemaNode.Components;

public interface IClusterEventScheduler
{
    void Schedule<T>(ClusterEvent<T> clusterEvent);
}