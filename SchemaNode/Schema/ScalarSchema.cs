namespace SchemaNode.Schema;

/**
 * The schema of the scalar type
*/
public class ScalarSchema
{
    /// <summary>
    /// The base type of the scalar
    /// </summary>
    public string? Base { get; set; }

    /// <summary>
    /// The default unit of the scalar value
    /// </summary>
    public LocaleString? Unit { get; set; }

    /// <summary>
    /// The default low limit of the scalar value
    /// </summary>
    public decimal? LowLimit { get; set; }

    /// <summary>
    /// The default up limit of the scalar value
    /// </summary>
    public decimal? UpLimit { get; set; }

    /// <summary>
    /// The default error message of the scalar value
    /// </summary>
    public LocaleString? Error  { get; set; }

    /// <summary>
    /// The regex of the scalar value
    /// </summary>
    public string? Regex  { get; set; }

    /// <summary>
    /// The function to validate the scalar value in frontend
    /// </summary>
    public string? PreValid  { get; set; }

    /// <summary>
    /// The eval function to convert the scalar value
    /// </summary>
    public string? PostValid  { get; set; }
}