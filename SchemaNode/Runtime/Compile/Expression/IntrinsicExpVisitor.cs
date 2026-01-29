using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using SchemaNode.Enum;
using SchemaNode.Function;
using SchemaNode.Node;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
using ExpressionType = SchemaNode.Enum.ExpressionType;

namespace SchemaNode.Runtime;

#region Intrinsic Expressions

/// <summary>
/// The break expression type
/// </summary>
public enum BreakExpType
{
    IfRet,
    IfNot,
    IfNull,
    IfEmpty,
}

/// <summary>
/// The attribute to mark a method as constant expression
/// </summary>
/// <param name="value"></param>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class ConstantAttribute(object value): System.Attribute
{
    public object Value { get; } = value;
}

/// <summary>
/// Represents a break operation expression composed of a condition and a value.
/// </summary>
/// <param name="Type">The break type</param>
/// <param name="Cond">The condition</param>
/// <param name="Value">The return value</param>
public record BreakExp(BreakExpType Type, SchemaExp Cond, SchemaExp Value) : SchemaExp(Value.SchemaType);

/// <summary>
/// The conditional expression
/// </summary>
/// <param name="Condition">The condition</param>
/// <param name="TrueExp">The true value</param>
/// <param name="FalseExp">The false value</param>
public record ConditionalExp(SchemaExp Condition, SchemaExp TrueExp, SchemaExp FalseExp, AnySchemaType SchemaType) : SchemaExp(SchemaType);

/// <summary>
/// Represents a schema expression that evaluates to a constant value.
/// </summary>
/// <param name="Value">The schema node representing the constant value for this expression. Cannot be null.</param>
public record ConstantExp(AnySchemaNode Value) : SchemaExp(Value.SchemaType);

/// <summary>
/// The default expression
/// </summary>
/// <param name="Inner"></param>
/// <param name="Default"></param>
public record DefaultExp(SchemaExp Inner, AnySchemaNode Default) : SchemaExp(Default.SchemaType);

/// <summary>
/// The null expression
/// </summary>
public record NullExp(AnySchemaType SchemaType) : SchemaExp(SchemaType);

/// <summary>
/// The field access expression
/// </summary>
/// <param name="Owner">The field owner</param>
/// <param name="FieldName">The field name</param>
/// <param name="SchemaType">The schema type</param>
public record FieldAccessExp(SchemaExp Owner, string FieldName, AnySchemaType SchemaType, ConstantExp? Default = null) : SchemaExp(SchemaType);

#endregion

/// <summary>
/// The assign expression visitor
/// </summary>
public class IntrinsicExpVisitor : IExpVisitor
{
    public int Priority => EXP_INTRINSIC_PRIORITY;

    // <inheritdoc/>
    public async Task<SchemaExp?> VisitExpAsync(CompileContext context, SchemaExp exp)
    {
        await Task.Yield();
        
        // Atomic function call expression check
        if (exp is not FuncCallExp { ExpType: ExpressionType.Call, Function:{ MethodInfo: { }} } callExp) return null;
        
        // Constant expression
        if (callExp.Function.MethodInfo.GetCustomAttribute<ConstantAttribute>() is { } constAttr)
            return new ConstantExp(callExp.SchemaType.CreateNode(constAttr.Value)!);
        
        switch (callExp.Function.Name)
        {
            // Assign expression
            case $"{NS_SYSTEM_CONV}.{nameof(SystemConv.assign)}":
                return callExp.Args.ElementAtOrDefault(0) 
                       ?? throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs);
            case $"{NS_SYSTEM_CONV}.{nameof(SystemConv.@default)}":
                return new DefaultExp(callExp.Args[0],
                    (callExp.Args.ElementAtOrDefault(1) as ConstantExp
                        ?? throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs))
                    .Value);
            case $"{NS_SYSTEM_CONV}.{nameof(SystemConv.@null)}":
                return new NullExp(callExp.SchemaType);
            
            // Break expressions
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.ifret)}":
                return new BreakExp(BreakExpType.IfRet, callExp.Args[0], callExp.Args[1]);
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.ifnot)}":
                return new BreakExp(BreakExpType.IfNot, callExp.Args[0], callExp.Args[1]);
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.ifnull)}":
                return new BreakExp(BreakExpType.IfNull, callExp.Args[0], callExp.Args[1]);
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.ifempty)}":
                return new BreakExp(BreakExpType.IfEmpty, callExp.Args[0], callExp.Args[1]);
            
            // Conditional expression
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.cond)}":
            {
                if (callExp.Args.Length != 3 || 
                    !(callExp.Args[0].SchemaType is ScalarType { IsBool: true }) ||
                    !callExp.Args[1].SchemaType.CanBeUseAs(exp.SchemaType) ||
                    !callExp.Args[2].SchemaType.CanBeUseAs(exp.SchemaType))
                    throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs);
                return new ConditionalExp(callExp.Args[0], callExp.Args[1], callExp.Args[2], exp.SchemaType);
            }
            
            // a[b]
            case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.getfield)}":
            {
                if (callExp.Args.Length != 2 || 
                    (callExp.Args[1] as ConstantExp)?.Value.ToValue<string>() is not { } fieldName || 
                    string.IsNullOrEmpty(fieldName))
                    throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs);

                return new FieldAccessExp(callExp.Args[0], fieldName, callExp.SchemaType);
            }
            
            // a[b] ?? defaultValue
            case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.getfielddefault)}":
            {
                if (callExp.Args.Length != 3 || 
                    (callExp.Args[1] as ConstantExp)?.Value.ToValue<string>() is not { } fieldName || 
                    string.IsNullOrEmpty(fieldName) || 
                    callExp.Args[2] is not ConstantExp defaultValueExp || 
                    defaultValueExp.Value.IsEmpty || 
                    !defaultValueExp.SchemaType.CanBeUseAs(exp.SchemaType))
                    throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs);

                return new FieldAccessExp(callExp.Args[0], fieldName, callExp.SchemaType, defaultValueExp);
            }
        }
        return null;
    }
    
    // <inheritdoc/>
    public async Task<Expression?> CompileExpAsync(CompileContext context, SchemaExp exp, Type expectedType)
    {
        await Task.Yield();
        
        switch (exp)
        {
            // Break
            case BreakExp breakExp:
            {
                Expression cond = await context.CompileSchemaExpAsync(breakExp.Cond);
                Expression value = await context.CompileSchemaExpAsync(breakExp.Value);
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
            
            // Constant
            case ConstantExp constExp:
            {
                // For reduce
                if (constExp.Value.IsEmpty && !expectedType.IsNullable())
                    return Expression.Default(expectedType);

                object? value = expectedType.GetNotNullType().TryConvert(constExp.Value);
                return value != null && value.GetType().IsSafeConstantValue()
                    ? Expression.Constant(value, expectedType)
                    : Expression.Default(expectedType);
            }
            
            // a ? b : c
            case ConditionalExp condExp:
                return Expression.Condition(await context.CompileSchemaExpAsync(condExp.Condition),
                    await context.CompileSchemaExpAsync(condExp.TrueExp, expectedType),
                    await context.CompileSchemaExpAsync(condExp.FalseExp, expectedType));
            
            // a ?? b
            case DefaultExp defaultExp:
            {
                if (defaultExp.Inner is LogicExp) return await context.CompileSchemaExpAsync(defaultExp.Inner, expectedType);
                var inner = await context.CompileSchemaExpAsync(defaultExp.Inner, expectedType);
                return inner.Type.IsNullable() ? Expression.Coalesce(inner,
                    Expression.Constant(expectedType.TryConvert(defaultExp.Default.Value), expectedType))
                        : inner;
            }
            
            // null
            case NullExp:
                return Expression.Constant(null, expectedType);

            // a[b]
            case FieldAccessExp fldAccess:
            {
                return fldAccess.Default != null
                    ? await context.CompileSchemaExpAsync(new FuncCallExp(
                        (await context.GetSchemaTypeAsync<FunctionType>($"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.getfielddefault)}"))!,
                        [fldAccess.Owner, new ConstantExp((await context.GetSchemaTypeAsync(NS_SYSTEM_STRING))!.CreateNode(fldAccess.FieldName)!), fldAccess.Default],
                        fldAccess.SchemaType
                    ), expectedType)
                    : await context.CompileSchemaExpAsync(new FuncCallExp(
                        (await context.GetSchemaTypeAsync<FunctionType>($"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.getfield)}"))!,
                        [fldAccess.Owner, new ConstantExp((await context.GetSchemaTypeAsync(NS_SYSTEM_STRING))!.CreateNode(fldAccess.FieldName)!)],
                        fldAccess.SchemaType
                    ), expectedType);
            }
        }

        return null;
    }
}