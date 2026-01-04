using System.Linq.Expressions;
using SchemaNode.Function;
using static SchemaNode.Utility.Constant;
using ExpressionType = SchemaNode.Enum.ExpressionType;

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
public abstract record ArithmeticExpression(AnySchemaType SchemaType) : SchemaExpression(SchemaType);

/// <summary>
/// The unary arithmetic expression
/// </summary>
/// <param name="Type">The arithmetic type</param>
/// <param name="Inner">The inner exp</param>
/// <param name="SchemaType">The result type</param>
public record UnaryArithmeticExpression(ArithmeticExpType Type, SchemaExpression Inner, AnySchemaType SchemaType) : ArithmeticExpression(SchemaType);

/// <summary>
/// The binary arithmetic expression
/// </summary>
/// <param name="Type">The arithmetic type</param>
/// <param name="Left">The left expression</param>
/// <param name="Right">The right expression</param>
/// <param name="SchemaType">The result type</param>
public record BinaryArithmeticExpression(ArithmeticExpType Type, SchemaExpression Left, SchemaExpression Right, AnySchemaType SchemaType) : ArithmeticExpression(SchemaType);

/// <summary>
/// The Arithmetic expression visitor
/// </summary>
public class ArithmeticExpressionVisitor : IExpressionVisitor
{
    // <inheritdoc/>
    public int Priority => EXP_ARITHMETIC_PRIORITY;

    // <inheritdoc/>
    public Task<SchemaExpression?> VisitExpAsync(CompileContext context, SchemaExpression exp)
    {
        if (exp is not FuncCallExpression { ExpType: ExpressionType.Call } callExp) return Task.FromResult<SchemaExpression?>(null);

        return Task.FromResult<SchemaExpression?>(callExp.Function.Name switch
        {
            // a + b
            $"{NS_SYSTEM_MATH}.{nameof(SystemMath.add)}" => new BinaryArithmeticExpression(ArithmeticExpType.Add, callExp.Args[0], callExp.Args[1], callExp.SchemaType),
            // a - b
            $"{NS_SYSTEM_MATH}.{nameof(SystemMath.subtract)}" => new BinaryArithmeticExpression(ArithmeticExpType.Subtract, callExp.Args[0], callExp.Args[1], callExp.SchemaType),
            // a / b
            $"{NS_SYSTEM_MATH}.{nameof(SystemMath.divide)}" => new BinaryArithmeticExpression(ArithmeticExpType.Divide, callExp.Args[0], callExp.Args[1], callExp.SchemaType),
            // a % b
            $"{NS_SYSTEM_MATH}.{nameof(SystemMath.modulo)}" => new BinaryArithmeticExpression(ArithmeticExpType.Modulo, callExp.Args[0], callExp.Args[1], callExp.SchemaType),
            // a * b
            $"{NS_SYSTEM_MATH}.{nameof(SystemMath.multiply)}" => new BinaryArithmeticExpression(ArithmeticExpType.Multiply, callExp.Args[0], callExp.Args[1], callExp.SchemaType),
            // Math.Min(a, b)
            $"{NS_SYSTEM_MATH}.{nameof(SystemMath.min)}" => new BinaryArithmeticExpression(ArithmeticExpType.Min, callExp.Args[0], callExp.Args[1], callExp.SchemaType),
            // Math.Max(a, b)
            $"{NS_SYSTEM_MATH}.{nameof(SystemMath.max)}" => new BinaryArithmeticExpression(ArithmeticExpType.Max, callExp.Args[0], callExp.Args[1], callExp.SchemaType),
            // Convert to decimal
            $"{NS_SYSTEM_MATH}.{nameof(SystemMath.todecimal)}" => new UnaryArithmeticExpression(ArithmeticExpType.ToDecimal, callExp.Args[0], callExp.SchemaType),
            // Convert to double
            $"{NS_SYSTEM_MATH}.{nameof(SystemMath.todouble)}" => new UnaryArithmeticExpression(ArithmeticExpType.ToDouble, callExp.Args[0], callExp.SchemaType),
            // Convert to single
            $"{NS_SYSTEM_MATH}.{nameof(SystemMath.tosingle)}" => new UnaryArithmeticExpression(ArithmeticExpType.ToSingle, callExp.Args[0], callExp.SchemaType),
            // Convert to int
            $"{NS_SYSTEM_MATH}.{nameof(SystemMath.tointeger)}" => new UnaryArithmeticExpression(ArithmeticExpType.ToInt, callExp.Args[0], callExp.SchemaType),
            // a & b
            $"{NS_SYSTEM_MATH}.{nameof(SystemMath.bitand)}" => new BinaryArithmeticExpression(ArithmeticExpType.BitAnd, callExp.Args[0], callExp.Args[1], callExp.SchemaType),
            // a | b
            $"{NS_SYSTEM_MATH}.{nameof(SystemMath.bitor)}" => new BinaryArithmeticExpression(ArithmeticExpType.BitOr, callExp.Args[0], callExp.Args[1], callExp.SchemaType),
            // a ^ b
            $"{NS_SYSTEM_MATH}.{nameof(SystemMath.bitxor)}" => new BinaryArithmeticExpression(ArithmeticExpType.BitXor, callExp.Args[0], callExp.Args[1], callExp.SchemaType),
            // a << shift
            $"{NS_SYSTEM_MATH}.{nameof(SystemMath.bitleftshift)}" => new BinaryArithmeticExpression(ArithmeticExpType.BitLeftShift, callExp.Args[0], callExp.Args[1], callExp.SchemaType),
            // a >> shift
            $"{NS_SYSTEM_MATH}.{nameof(SystemMath.bitrightshift)}" => new BinaryArithmeticExpression(ArithmeticExpType.BitRightShift, callExp.Args[0], callExp.Args[1], callExp.SchemaType),
            // ~a
            $"{NS_SYSTEM_MATH}.{nameof(SystemMath.bitunary)}" => new UnaryArithmeticExpression(ArithmeticExpType.BitUnary, callExp.Args[0], callExp.SchemaType),
            _ => null
        });
    }

    // <inheritdoc/>
    public async Task<Expression?> CompileExpAsync(CompileContext context, SchemaExpression exp)
    {
        if (exp is not ArithmeticExpression arithmeticExp) return null;
        switch (arithmeticExp)
        {
            case UnaryArithmeticExpression unaryExp:
            {
                Expression innerExp = await context.CompileSchemaExpAsync(unaryExp.Inner);
                return unaryExp.Type switch
                {
                    ArithmeticExpType.ToDecimal => Expression.Convert(innerExp, typeof(decimal)),
                    ArithmeticExpType.ToDouble => Expression.Convert(innerExp, typeof(double)),
                    ArithmeticExpType.ToSingle => Expression.Convert(innerExp, typeof(float)),
                    ArithmeticExpType.ToInt => Expression.Convert(innerExp, typeof(int)),
                    ArithmeticExpType.BitUnary => Expression.OnesComplement(innerExp),
                    _ => throw new NotSupportedException($"Unsupported unary arithmetic expression type: {unaryExp.Type}")
                };
            }
            case BinaryArithmeticExpression binaryExp:
            {
                Expression leftExp = await context.CompileSchemaExpAsync(binaryExp.Left);
                Expression rightExp = await context.CompileSchemaExpAsync(binaryExp.Right);
                return binaryExp.Type switch
                {
                    ArithmeticExpType.Add => Expression.Add(leftExp, rightExp),
                    ArithmeticExpType.Subtract => Expression.Subtract(leftExp, rightExp),
                    ArithmeticExpType.Divide => Expression.Divide(leftExp, rightExp),
                    ArithmeticExpType.Modulo => Expression.Modulo(leftExp, rightExp),
                    ArithmeticExpType.Multiply => Expression.Multiply(leftExp, rightExp),
                    ArithmeticExpType.Min => Expression.Call(typeof(Math).GetMethod(nameof(Math.Min), [leftExp.Type, rightExp.Type])!, leftExp, rightExp),
                    ArithmeticExpType.Max =>  Expression.Call(typeof(Math).GetMethod(nameof(Math.Max), [leftExp.Type, rightExp.Type])!, leftExp, rightExp),
                    ArithmeticExpType.BitAnd => Expression.And(leftExp, rightExp),
                    ArithmeticExpType.BitOr => Expression.Or(leftExp, rightExp),
                    ArithmeticExpType.BitXor => Expression.ExclusiveOr(leftExp, rightExp),
                    ArithmeticExpType.BitLeftShift => Expression.LeftShift(leftExp, rightExp),
                    ArithmeticExpType.BitRightShift => Expression.RightShift(leftExp, rightExp),
                    _ => throw new NotSupportedException($"Unsupported binary arithmetic expression type: {binaryExp.Type}")
                };
            }
            default:
                throw new NotSupportedException($"Unsupported arithmetic expression type: {arithmeticExp.GetType().Name}");
        }
    }
}
