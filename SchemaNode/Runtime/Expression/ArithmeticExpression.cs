using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Function;
using static SchemaNode.Utility.Constant;
// ReSharper disable NotAccessedPositionalProperty.Global

namespace SchemaNode.Runtime;

/// <summary>
/// The Arithmetic exp type
/// </summary>
public enum ArithmeticExpType
{
    // Math
    Add,
    Subtract,
    Divide,
    Modulo,
    Multiply,
    Min,
    Max,

    // Bit
    BitUnary,
    BitAnd,
    BitLeftShift,
    BitOr,
    BitRightShift,
    BitXor,

    // Conv
    ToDecimal,
    ToDouble,
    ToSingle,
    ToInt,
}

/// <summary>
/// The arithmetic expression
/// </summary>
/// <param name="SchemaType"></param>
public abstract record ArithmeticExpression(AnySchemeType SchemaType) : SchemaExpression(SchemaType);

/// <summary>
/// The unary arithmetic expression
/// </summary>
/// <param name="Type">The arithmetic type</param>
/// <param name="Inner">The inner exp</param>
/// <param name="SchemaType">The result type</param>
public record UnaryArithmeticExpression(ArithmeticExpType Type, SchemaExpression Inner, AnySchemeType SchemaType) : ArithmeticExpression(SchemaType);

/// <summary>
/// The binary arithmetic expression
/// </summary>
/// <param name="Type">The arithmetic type</param>
/// <param name="Left">The left expression</param>
/// <param name="Right">The right expression</param>
/// <param name="SchemaType">The result type</param>
public record BinaryArithmeticExpression(ArithmeticExpType Type, SchemaExpression Left, SchemaExpression Right, AnySchemeType SchemaType) : ArithmeticExpression(SchemaType);

/// <summary>
/// The Arithmetic expression visitor
/// </summary>
public class ArithmeticExpressionVisitor : IExpressionVisitor
{
    // <inheritdoc/>
    public int Priority => EXP_ARITHMETIC_PRIORITY;

    // <inheritdoc/>
    public SchemaExpression? VisitExpression(SchemaContext context, SchemaExpression exp)
    {
        if (exp is not FuncCallExpression { ExpType: ExpressionType.Call } callExp) return null;

        return callExp.Function.Name switch
        {
            // a + b
            $"{NS_SYSTEM_MATH}.{nameof(SystemMath.add)}" => new BinaryArithmeticExpression(ArithmeticExpType.Add, callExp.Args[0], callExp.Args[1], callExp.SchemeType),
            // a - b
            $"{NS_SYSTEM_MATH}.{nameof(SystemMath.subtract)}" => new BinaryArithmeticExpression(ArithmeticExpType.Subtract, callExp.Args[0], callExp.Args[1], callExp.SchemeType),
            // a / b
            $"{NS_SYSTEM_MATH}.{nameof(SystemMath.divide)}" => new BinaryArithmeticExpression(ArithmeticExpType.Divide, callExp.Args[0], callExp.Args[1], callExp.SchemeType),
            // a % b
            $"{NS_SYSTEM_MATH}.{nameof(SystemMath.modulo)}" => new BinaryArithmeticExpression(ArithmeticExpType.Modulo, callExp.Args[0], callExp.Args[1], callExp.SchemeType),
            // a * b
            $"{NS_SYSTEM_MATH}.{nameof(SystemMath.multiply)}" => new BinaryArithmeticExpression(ArithmeticExpType.Multiply, callExp.Args[0], callExp.Args[1], callExp.SchemeType),
            // Math.Min(a, b)
            $"{NS_SYSTEM_MATH}.{nameof(SystemMath.min)}" => new BinaryArithmeticExpression(ArithmeticExpType.Min, callExp.Args[0], callExp.Args[1], callExp.SchemeType),
            // Math.Max(a, b)
            $"{NS_SYSTEM_MATH}.{nameof(SystemMath.max)}" => new BinaryArithmeticExpression(ArithmeticExpType.Max, callExp.Args[0], callExp.Args[1], callExp.SchemeType),
            // Convert to decimal
            $"{NS_SYSTEM_MATH}.{nameof(SystemMath.todecimal)}" => new UnaryArithmeticExpression(ArithmeticExpType.ToDecimal, callExp.Args[0], callExp.SchemeType),
            // Convert to double
            $"{NS_SYSTEM_MATH}.{nameof(SystemMath.todouble)}" => new UnaryArithmeticExpression(ArithmeticExpType.ToDouble, callExp.Args[0], callExp.SchemeType),
            // Convert to single
            $"{NS_SYSTEM_MATH}.{nameof(SystemMath.tosingle)}" => new UnaryArithmeticExpression(ArithmeticExpType.ToSingle, callExp.Args[0], callExp.SchemeType),
            // Convert to int
            $"{NS_SYSTEM_MATH}.{nameof(SystemMath.tointeger)}" => new UnaryArithmeticExpression(ArithmeticExpType.ToInt, callExp.Args[0], callExp.SchemeType),
            // a & b
            $"{NS_SYSTEM_MATH}.{nameof(SystemMath.bitand)}" => new BinaryArithmeticExpression(ArithmeticExpType.BitAnd, callExp.Args[0], callExp.Args[1], callExp.SchemeType),
            // a | b
            $"{NS_SYSTEM_MATH}.{nameof(SystemMath.bitor)}" => new BinaryArithmeticExpression(ArithmeticExpType.BitOr, callExp.Args[0], callExp.Args[1], callExp.SchemeType),
            // a ^ b
            $"{NS_SYSTEM_MATH}.{nameof(SystemMath.bitxor)}" => new BinaryArithmeticExpression(ArithmeticExpType.BitXor, callExp.Args[0], callExp.Args[1], callExp.SchemeType),
            // a << shift
            $"{NS_SYSTEM_MATH}.{nameof(SystemMath.bitleftshift)}" => new BinaryArithmeticExpression(ArithmeticExpType.BitLeftShift, callExp.Args[0], callExp.Args[1], callExp.SchemeType),
            // a >> shift
            $"{NS_SYSTEM_MATH}.{nameof(SystemMath.bitrightshift)}" => new BinaryArithmeticExpression(ArithmeticExpType.BitRightShift, callExp.Args[0], callExp.Args[1], callExp.SchemeType),
            // ~a
            $"{NS_SYSTEM_MATH}.{nameof(SystemMath.bitunary)}" => new UnaryArithmeticExpression(ArithmeticExpType.BitUnary, callExp.Args[0], callExp.SchemeType),
            _ => null
        };
    }
}
