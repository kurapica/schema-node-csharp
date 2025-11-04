namespace SchemaNode.Components;

public interface IApplicationEventScheduler
{
    void Schedule<T>(ApplicationEvent<T> applicationEvent);
}