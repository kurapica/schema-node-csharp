namespace SchemaNode.Components;

/// <summary>
/// The application data event, normally for target app data access
/// </summary>
/// <param name="app"></param>
/// <param name="target"></param>
public abstract class AppDataEvent(string app, string target): AppEvent(app)
{
    /// <summary>
    /// The topic
    /// </summary>
    public override string Topic => $"{base.Topic}/{target}";
}
