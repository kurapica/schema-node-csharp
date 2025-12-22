using SchemaNode.Context;
using System.Reflection;

namespace SchemaNode.Runtime;

/// <summary>
/// The binary exp type
/// </summary>
public enum BinaryExpType
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
    BitAnd,
    BitLeftShift,
    BitOr,
    BitRightShift,
    BitXor,

    // Logic
    IfRet,

    AndAlso,
    OrElse,

    // Compare
    Equal,
    NotEqual,
    GreaterThan,
    GreaterEqual,
    LessThan,
    LessEqual,

    // String
    Concat,
    Split,
    StartsWith,
    EndsWith,

    // Collections
    Contains,
    NotContains,
    
    FieldAccess,
}

/// <summary>
/// Represents a binary operation expression composed of two schema expressions and a specified operation type.
/// </summary>
/// <param name="Type">The type of binary operation to apply between the left and right expressions.</param>
/// <param name="Left">The left operand of the binary expression.</param>
/// <param name="Right">The right operand of the binary expression.</param>
/// <param name="SchemaType">The schema type associated with the resulting expression.</param>
public record BinaryExpression(BinaryExpType Type, SchemaExpression Left, SchemaExpression Right, AnySchemeType SchemaType) : SchemaExpression(SchemaType);

/// <summary>
/// The binary expression attribute
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public class BinaryExpAttribute(BinaryExpType type): System.Attribute
{
    /// <summary>
    /// The binary expression type
    /// </summary>
    public BinaryExpType Type { get; } = type;
}

/// <summary>
/// The binary expression visitor
/// </summary>
public class BinaryExpressionVisitor : IExpressionVisitor
{
    public int Priorty => 100;

    // <inheritdoc/>
    public SchemaExpression? VisitExpression(SchemaContext context, FuncCallExpression funcCallExp)
    {
        var attr = funcCallExp.Function?.FuncInfo?.Method?.GetCustomAttribute<BinaryExpAttribute>();
        return attr != null && funcCallExp.Args.Length >= 2 ? new BinaryExpression(attr.Type, funcCallExp.Args[0], funcCallExp.Args[1], funcCallExp.SchemaType) : null;
    }
}