namespace SchemaNode.Event;

/// <summary>
/// The application scope event
/// </summary>
public abstract class AppEvent(string app): Event
{
    /// <summary>
    /// The topic
    /// </summary>
    public override string Topic => app.ToLower().Replace(".", "_");
}
