namespace SchemaNode.Enum;

/// <summary>
/// Workflow type
/// </summary>
public enum Workflow
{
    /// <summary>
    /// The interval workflow
    /// </summary>
    Interval,
    
    /// <summary>
    /// The cron workflow
    /// </summary>
    Cron,
    
    /// <summary>
    /// The event triggered workflow
    /// </summary>
    Event,
    
    /// <summary>
    /// Call function workflow
    /// </summary>
    Function,
    
    /// <summary>
    /// Workflow control type
    /// </summary>
    WorkflowControl,
}