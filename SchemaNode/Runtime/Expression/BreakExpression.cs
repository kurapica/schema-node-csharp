using SchemaNode.Context;
using System.Reflection;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;

public enum BreakExpType
{
    IfRet,
    IfNot,
    IfNull,
    IfEmpty,
}

/// <summary>
/// Represents a break operation expression composed of a condition and a value.
/// </summary>
/// <param name="Type">The break type</param>
/// <param name="Cond">The condition</param>
/// <param name="Value">The return value</param>
public record BreakExpression(BreakExpType Type, SchemaExpression Cond, SchemaExpression Value) : SchemaExpression(Value.SchemaType);

/// <summary>
/// The binary expression attribute
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public class BreakExpAttribute(BreakExpType type) : System.Attribute
{
    /// <summary>
    /// The binary expression type
    /// </summary>
    public BreakExpType Type { get; } = type;
}

/// <summary>
/// The binary expression visitor
/// </summary>
public class BreakExpTypeVisitor : IExpressionVisitor
{
    public int Priority => EXP_BREAK_PRIORITY;

    // <inheritdoc/>
    public SchemaExpression? VisitExpression(SchemaContext context, SchemaExpression exp)
    {
        if (exp is not FuncCallExpression callExp) return null;
        var attr = callExp.Function?.FuncInfo?.Method?.GetCustomAttribute<BreakExpAttribute>();
        return attr != null && callExp.Args.Length >= 2 ? new BreakExpression(attr.Type, callExp.Args[0], callExp.Args[1]) : null;
    }
}