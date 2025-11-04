namespace SchemaNode.Components;

public interface IServerEventScheduler
{
    void Schedule<T>(ServerEvent<T> serverEvent);
}