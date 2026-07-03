namespace SchemaNode.Enum;

/// <summary>
/// The logic operation type
/// </summary>
public enum LogicType
{
    // CombineProperties
    AndAlso,
    OrElse,
    Not,

    // Null / Empty
    IsNull,
    IsEmpty,
    NotNull,
    NotEmpty,

    // Compare
    Equal,
    NotEqual,
    GreaterThan,
    GreaterEqual,
    LessThan,
    LessEqual,

    // Collections
    Contains,
    NotContains,

    // String
    StartsWith,
    NotStartsWith,
    EndsWith,
    NotEndsWith,
    Match,
    NotMatch,
}
