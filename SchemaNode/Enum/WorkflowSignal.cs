namespace SchemaNode.Enum;

/// <summary>
/// The workflow action enum
/// </summary>
public enum WorkflowSignal
{
    /// <summary>
    /// The start signal
    /// </summary>
    Start = 1,
    
    /// <summary>
    /// The pause signal
    /// </summary>
    Pause,
    
    /// <summary>
    /// The resume signal
    /// </summary>
    Resume,
    
    /// <summary>
    /// The done signal
    /// </summary>
    Done,
    
    /// <summary>
    /// The rollback signal
    /// </summary>
    Rollback,
    
    /// <summary>
    /// The terminate signal
    /// </summary>
    Terminate
}