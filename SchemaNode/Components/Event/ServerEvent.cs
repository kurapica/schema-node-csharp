namespace SchemaNode.Components;

/// <summary>
/// The server scope event
/// </summary>
public abstract class ServerEvent: Event
{
}

public interface IServerEventDispatcher: IEventDispatcher<ServerEvent>
{
}