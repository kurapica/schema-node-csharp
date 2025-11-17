namespace SchemaNode.Components.AppInteractionEvent;

/// <summary>
/// The user interaction within app event
/// </summary>
public class AppInteractionEvent(string app): AppEvent(app), IEventPayload
{
}