namespace SchemaNode.Property.App;

/// <summary>
/// The push settings
/// </summary>
public class Push : Property<PushSource>
{
    public override void SetValue<TValue>(TValue value)
    {
        string push = string.Empty;
        string source = string.Empty;
        if (value is object[] objs)
        {
            push = objs.ElementAtOrDefault(0)?.ToString() ?? string.Empty;
            source = objs.ElementAtOrDefault(1)?.ToString() ?? string.Empty;
        }

        if (!string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(push))
            base.SetValue(new PushSource(push, source));
    }
}

/// <summary>
/// The push function and the data source
/// </summary>
/// <param name="Push">The push function</param>
/// <param name="Source">The data source</param>
public record PushSource(string Push, string Source);