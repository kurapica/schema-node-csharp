namespace SchemaNode.Enum;

/// <summary>
/// The arithmetic operation type
/// </summary>
public enum ArithmeticType
{
    // Basic Math
    Add,
    Subtract,
    Divide,
    Modulo,
    Multiply,
    Min,
    Max,

    // Bitwise
    BitUnary,
    BitAnd,
    BitLeftShift,
    BitOr,
    BitRightShift,
    BitXor,

    // Conversion
    ToDecimal,
    ToDouble,
    ToSingle,
    ToInt,

    // Others
    Transform,
}
