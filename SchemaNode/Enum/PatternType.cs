using SchemaNode.Attribute;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Enum;

/// <summary>
/// The pattern part type used by Pattern for cross-platform pattern validation.
/// Inspired by Lua patterns — a simplified, multi-platform alternative to regex.
/// </summary>
[Schema($"{NS_SYSTEM_SCHEMA_DEF_RECOGNIZER}.patterntype")]
public enum PatternType
{
    /// <summary>
    /// Match literal text exactly.
    /// Example: "abc" matches the exact string "abc".
    /// When Min = 0, the literal is optional.
    /// </summary>
    Literal,

    /// <summary>
    /// Match characters from a defined set (ranges and/or specific chars).
    /// Example: [0-9a-z] matches any digit or lowercase letter.
    /// </summary>
    CharSet,

    /// <summary>
    /// Match any single character (wildcard).
    /// Equivalent to "." in Lua patterns.
    /// </summary>
    Any,

    /// <summary>
    /// Match a sub-pattern sequence as a single unit with its own Min/Max.
    /// The sub-pattern is defined via <see cref="Schema.Pattern.Parts"/>.
    /// Example: optional decimal part "(\\.\\d+)?" → Group(Parts=[Literal("."), CharSet(0-9,1+)], Min=0, Max=1)
    /// </summary>
    Group,
}
