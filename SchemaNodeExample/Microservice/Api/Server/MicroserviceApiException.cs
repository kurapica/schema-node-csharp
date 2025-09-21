namespace SchemaNode.Example;

/// <summary>
/// Thrown when the request argument is invalid of a microservice API.
/// </summary>
public class MicroserviceApiException : Exception
{
    #region Constructors

    /// <summary>
    /// The microservice api exception
    /// </summary>
    public MicroserviceApiException(MicroserviceApiResponseErrorCode code, string message, string? messageKey = null, IDictionary<string, object>? data = null, Exception? innerException = null) : base(message, innerException)
    {
        Code = code;
        MessageKey = messageKey;
        AdditionalData = data;
    }

    #endregion

    #region Error Messages

    /// <summary>
    /// Gets the code.
    /// </summary>
    public MicroserviceApiResponseErrorCode Code { get; }

    /// <summary>
    /// Gets the key, if any.
    /// </summary>
    public string? MessageKey { get; }

    /// <summary>
    /// Gets the additional data.
    /// </summary>
    public IDictionary<string, object>? AdditionalData { get; }

    #endregion
}