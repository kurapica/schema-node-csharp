namespace SchemaNode.Config;

/// <summary>
/// The scalar config
/// </summary>
public interface IScalarConfig: ISchemaConfig
{
    /// <summary>
    /// The white list
    /// </summary>
    public string[]? WhiteList { get; set; }

    /// <summary>
    /// The root value, for special scalar type values
    /// </summary>
    public string? Root { get; set; }
    
    /// <summary>
    /// The black list
    /// </summary>
    public string[]? BlackList { get; set; }

    /// <summary>
    /// The low limit of the scalar value.
    /// </summary>
    public string? LowLimit { get; set; }

    /// <summary>
    /// The up limit of the scalar value.
    /// </summary>
    public string? UpLimit { get; set; }

    /// <summary>
    /// The enum white list only used for suggest.
    /// </summary>
    public bool? AsSuggest { get; set; }

    /// <summary>
    /// When calculating the up limit, use the original value.
    /// </summary>
    public bool? UseOriginForUplimit { get; set; }
}