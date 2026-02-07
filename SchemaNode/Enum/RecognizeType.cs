namespace SchemaNode.Enum;

/// <summary>
/// The recognize type
/// </summary>
public enum RecognizeType
{
    /// <summary>
    /// Match literal text
    /// </summary>
    Literal,

    /// <summary>
    /// Match a character set
    /// </summary>
    Charset,

    /// <summary>
    /// Match any single character
    /// </summary>
    Any,

    /// <summary>
    /// Call a function for validation or conversion
    /// </summary>
    Function,

    /// <summary>
    /// Choose one branch from Nexts
    /// </summary>
    Branch,

    /// <summary>
    /// Group nested steps under Steps
    /// </summary>
    Group,
}