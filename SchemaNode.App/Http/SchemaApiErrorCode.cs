namespace SchemaNode.App.Http;

/// <summary>
/// The schema api error code
/// </summary>
public enum SchemaApiErrorCode
{
    /// <summary>No error</summary>
    None = 0,

    /// <summary>Parse request failed</summary>
    ParseFailed = 1,

    /// <summary>Invalid parameters</summary>
    InvalidParams = 2,

    /// <summary>Internal error</summary>
    InternalError = 3,
}
