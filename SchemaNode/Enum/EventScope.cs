namespace SchemaNode.Enum;

/// <summary>
/// The system event type
/// </summary>
public enum EventScope
{
    /// <summary>
    /// The workflow event
    /// </summary>
    Workflow = 1,
    
    /// <summary>
    /// The application event
    /// </summary>
    Application,
    
    /// <summary>
    /// The server event
    /// </summary>
    Server,
    
    /// <summary>
    /// The cluster event
    /// </summary>
    Cluster
}