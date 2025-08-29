namespace SchemaNode.Config;

/// <summary>
/// The enum config
/// </summary>
public interface IEnumConfig: ISchemaConfig
{
    /// <summary>
    /// The enum cascade limit.
    /// </summary>
    public int? Cascade { get; set; }

    /// <summary>
    /// The enum root value.
    /// </summary>
    public string? Root { get; set; }

    /// <summary>
    /// The enum white list
    /// </summary>
    public string[]? WhiteList { get; set; }

    /// <summary>
    /// The enum black list
    /// </summary>
    public string[]? BlackList { get; set; }

    /// <summary>
    /// Allow use enum value in any level.
    /// </summary>
    public bool? AnyLevel { get; set; }

    /// <summary>
    /// Don't allow flags enum value combination.
    /// </summary>
    public bool? SingleFlag { get; set; }
}