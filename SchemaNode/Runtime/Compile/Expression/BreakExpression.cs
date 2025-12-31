using System.Linq.Expressions;
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
    
    // <inheritdoc/>
    public Expression? CompileExpression(CompileContext context, SchemaExpression exp)
    {
        if (exp is not BreakExpression breakExp) return null;
        
        Expression cond = context.CompileSchemaExpression(breakExp.Cond);
        Expression value = context.CompileSchemaExpression(breakExp.Value);
        ParameterExpression resultVar = Expression.Variable(value.Type);

        return breakExp.Type switch
        {
            // if
            BreakExpType.IfRet => Expression.Block([resultVar], Expression.Assign(resultVar, value),
                Expression.IfThen(cond, Expression.Return(context.GetReturnLabel()!, resultVar)), resultVar),
            
            // if not
            BreakExpType.IfNot => Expression.Block([resultVar], Expression.Assign(resultVar, value),
                Expression.IfThen(Expression.Not(cond), Expression.Return(context.GetReturnLabel()!, resultVar)),
                resultVar),
            
            // if null
            BreakExpType.IfNull => Expression.Block([resultVar], Expression.Assign(resultVar, value),
                Expression.IfThen(Expression.Equal(cond, Expression.Constant(null, cond.Type)),
                    Expression.Return(context.GetReturnLabel()!, resultVar)), resultVar),
            
            // if empty
            BreakExpType.IfEmpty => Expression.Block([resultVar], Expression.Assign(resultVar, value),
                Expression.IfThen(Expression.Call(typeof(SystemLogic).GetMethod(nameof(SystemLogic.isempty))!, cond),
                    Expression.Return(context.GetReturnLabel()!, resultVar)), resultVar),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}