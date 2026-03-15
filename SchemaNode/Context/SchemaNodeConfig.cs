using SchemaNode.Components;

namespace SchemaNode.Context;

public class SchemaNodeConfig
{
    /// <summary>
    /// The default time zone
    /// </summary>
    public string? TimeZone { get; set; }

    /// <summary>
    /// The max take count for increment field
    /// </summary>
    public int IncrFieldMaxTakeCount { get; set; } = 50;
    
    /// <summary>
    /// The default take count for increment field
    /// </summary>
    public int IncrFieldDefaultTakeCount { get; set; } = 20;
    
    /// <summary>
    /// The max concurrent threads for quartz scheduler
    /// </summary>
    public int MaxQuartzConcurrentThreads { get; set; } = 10;

    /// <summary>Gets the currently active options instance.</summary>
    internal static SchemaNodeConfig Current { get; private set; } = new();

    /// <summary>Stores <paramref name="options"/> as the active instance.</summary>
    internal static void Apply(SchemaNodeConfig options)
    {
        Current = options;

        if (!string.IsNullOrWhiteSpace(options.TimeZone))
            AccessContextItemProviderExtensions.SetDefaultTimeZone(options.TimeZone);
    }
}