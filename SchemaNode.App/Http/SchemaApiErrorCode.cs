namespace SchemaNode.Http;

/// <summary>
/// The schema api error code
/// </summary>
public enum SchemaApiErrorCode
{
    /// <summary>
    /// No error
    /// </summary>
    None = 0,
    
    /// <summary>
    /// parse request failed
    /// </summary>
    ParseFailed = 1,
    
    /// <summary>
    /// Invalid parameters
    /// </summary>
    InvalidParams = 2,
    
    /// <summary>
    /// internal error
    /// </summary>
    InternalError = 3,
}