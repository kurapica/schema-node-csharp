using SchemaNode.Components;

namespace SchemaNode.Runtime;

/// <summary>
/// The unary exp type
/// </summary>
public enum UnaryExpType
{
    // Conv
    Assign,
    Default,
    Null,

    // Math
    Abs,
    Ceiling,
    Floor,

    // Bit
    BitUnary,

    // Conv
    ToDecimal,
    ToDouble,
    ToSingle,
    ToInt,

    // Logic
    Not,

    IsNull,
    IsEmpty,

    NotNull,
    NotEmpty,

    // String
    Length,
}

/// <summary>
/// Represents a unary operation applied to a schema expression within a specified schema type.
/// </summary>
/// <param name="Type">The type of unary operation to apply to the inner expression.</param>
/// <param name="Inner">The schema expression to which the unary operation is applied.</param>
/// <param name="SchemaType">The schema type associated with the resulting expression.</param>
public record UnaryExpression(UnaryAccessExpType Type, SchemaExpression Inner, AnySchemeType SchemaType) : SchemaExpression(SchemaType);

/// <summary>
/// The binary expression attribute
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public class UnaryExpAttribute(UnaryExpType type) : System.Attribute
{
    /// <summary>
    /// The unary expression type
    /// </summary>
    public UnaryExpType Type { get; } = type;
}