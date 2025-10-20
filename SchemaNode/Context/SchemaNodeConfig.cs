namespace SchemaNode.Context;

public class SchemaNodeConfig
{
    /// <summary>
    /// The time zone
    /// </summary>
    public string TimeZone { get; set; } = "China Standard Time";

    /// <summary>
    /// The max take count for increment field
    /// </summary>
    public int IncrFieldMaxTakeCount { get; set; } = 1000;
    
    /// <summary>
    /// The default take count for increment field
    /// </summary>
    public int IncrFieldDefaultTakeCount { get; set; } = 50;
}