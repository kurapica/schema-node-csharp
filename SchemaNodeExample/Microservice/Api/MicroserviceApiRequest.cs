using System.Text.Json.Serialization;

namespace SchemaNode.Example;

/// <summary>
/// Contains the base implementation of a microservice API request.
/// </summary>
public abstract class MicroserviceApiRequest
{
    #region Cancel Token

    /// <summary>
    /// Cancel token
    /// </summary>
    [JsonIgnore]
    public CancellationToken CancellationToken { get; set; }

    #endregion
}