namespace SchemaNode.Runtime;

/// <summary>
/// The ternary expression type
/// </summary>
public enum TernaryExpType
{
    // Math
    Clamp,

    // Logic
    Conditional,

    // String
    Substr,
    Replace,

    // Collection
    FieldAccess,
}

/// <summary>
/// Represents a ternary expression composed of three schema expressions and an associated ternary operation type.
/// </summary>
/// <param name="Type">The type of ternary operation to apply to the expressions.</param>
/// <param name="First">The first operand of the ternary expression.</param>
/// <param name="Second">The second operand of the ternary expression.</param>
/// <param name="Third">The third operand of the ternary expression.</param>
/// <param name="SchemeType">The schema type associated with the resulting expression.</param>
public record TernaryExpression(TernaryExpType Type, SchemaExpression First, SchemaExpression Second, SchemaExpression Third, AnySchemeType SchemeType) : SchemaExpression(SchemeType);

/// <summary>
/// The ternary expression attribute
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public class TernaryExpAttribute(TernaryExpType type) : System.Attribute
{
    /// <summary>
    /// The ternary expression type
    /// </summary>
    public TernaryExpType Type { get; } = type;
}