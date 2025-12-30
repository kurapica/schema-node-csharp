using SchemaNode.Context;
using System.Reflection;
using SchemaNode.Function;
using static SchemaNode.Utility.Constant;
// ReSharper disable NotAccessedPositionalProperty.Global

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
/// The binary expression visitor
/// </summary>
public class BreakExpTypeVisitor : IExpressionVisitor
{
    public int Priority => EXP_BREAK_PRIORITY;

    // <inheritdoc/>
    public SchemaExpression? VisitExpression(SchemaContext context, SchemaExpression exp)
    {
        if (exp is not FuncCallExpression callExp) return null;
        return callExp.Function.Name switch
        {
            $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.ifret)}" => new BreakExpression(BreakExpType.IfRet, callExp.Args[0], callExp.Args[1]),
            $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.ifnot)}" => new BreakExpression(BreakExpType.IfNot, callExp.Args[0], callExp.Args[1]),
            $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.ifnull)}" => new BreakExpression(BreakExpType.IfNull,callExp.Args[0], callExp.Args[1]),
            $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.ifempty)}" => new BreakExpression(BreakExpType.IfEmpty, callExp.Args[0], callExp.Args[1]),
            _ => null
        };
    }
}