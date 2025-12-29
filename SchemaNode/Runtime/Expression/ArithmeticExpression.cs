using SchemaNode.Context;
using System.Reflection;
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

    // Call
    Func,
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
/// The func call arithmetic expression
/// </summary>
/// <param name="Function">The arithmetic func</param>
/// <param name="Args">The params arguments</param>
/// <param name="SchemaType">The result type</param>
public record FuncArithmeticExpression(FunctionType Function, SchemaExpression[] Args, AnySchemeType SchemaType) : ArithmeticExpression(SchemaType);

/// <summary>
/// The Arithmetic expression attribute
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public class ArithmeticExpAttribute(ArithmeticExpType type = ArithmeticExpType.Func) : System.Attribute
{
    /// <summary>
    /// The binary expression type
    /// </summary>
    public ArithmeticExpType Type { get; } = type;
}

/// <summary>
/// The Arithmetic expression visitor
/// </summary>
public class ArithmeticExpressionVisitor : IExpressionVisitor
{
    public int Priority => EXP_ARITHMETIC_PRIORITY;

    // <inheritdoc/>
    public SchemaExpression? VisitExpression(SchemaContext context, SchemaExpression exp)
    {
        if (exp is not FuncCallExpression callExp) return null;
        var attr = callExp.Function.FuncInfo?.Method?.GetCustomAttribute<ArithmeticExpAttribute>();
        if (attr == null) return null;

        // Keep function call but change to arithmetic expression
        if (attr.Type == ArithmeticExpType.Func)
            return new FuncArithmeticExpression(callExp.Function, callExp.Args, callExp.SchemeType);

        // Unary or binary expression
        return callExp.Args.Length switch
        {
            1 => new UnaryArithmeticExpression(attr.Type, callExp.Args[0], callExp.SchemeType),
            2 => new BinaryArithmeticExpression(attr.Type, callExp.Args[0], callExp.Args[1], callExp.SchemeType),
            _ => null
        };
    }
}
