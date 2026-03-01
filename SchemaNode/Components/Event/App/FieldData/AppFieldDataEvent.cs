namespace SchemaNode.Components;

/// <summary>
/// The application field data event, normally for specific field data update
/// </summary>
/// <param name="app"></param>
/// <param name="target"></param>
/// <param name="field"></param>
public abstract class AppFieldDataEvent(string app, string target, string @field): AppDataEvent(app, target)
{
    public override string Topic => $"{base.Topic}/{@field}";
}