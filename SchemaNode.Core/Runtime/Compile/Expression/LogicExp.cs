using SchemaNode.Function;
using System.Linq.Expressions;
using System.Reflection;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

// ReSharper disable NotAccessedPositionalProperty.Global

namespace SchemaNode.Runtime;

#region Logic Exp

/// <summary>
/// The logic exp type
/// </summary>
[AttributeUsage( AttributeTargets.Method)]
public class LogicAttribute(LogicType type, bool includeMethod = false): System.Attribute
{
    /// <summary>
    /// The logic exp type
    /// </summary>
    public LogicType Type { get; } = type;
    
    /// <summary>
    /// Include the method info
    /// </summary>
    public bool IncludeMethod { get; } = includeMethod;
}

/// <summary>
/// The logic expression
/// </summary>
/// <param name="Type">The logic exp type</param>
/// <param name="ValueType">The result type(bool only)</param>
public abstract record LogicExp(LogicType Type, ValueType ValueType) : SchemaExp(ValueType);

/// <summary>
/// The unary logic expression
/// </summary>
/// <param name="Type">The logic exp type</param>
/// <param name="Inner">The inner exp</param>
/// <param name="ValueType">The result type(bool only)</param>
/// <param name="Method">The method if require</param>
public record UnaryLogicExp(LogicType Type, SchemaExp Inner, ValueType ValueType, FunctionType? Method = null) : LogicExp(Type, ValueType);

/// <summary>
/// The binary logic expression
/// </summary>
/// <param name="Type">The logic exp type</param>
/// <param name="Left">The left exp</param>
/// <param name="Right">The right exp</param>
/// <param name="ValueType">The result type(bool only)</param>
/// <param name="Method">The method if require</param>
public record BinaryLogicExp(LogicType Type, SchemaExp Left, SchemaExp Right, ValueType ValueType, FunctionType? Method = null) : LogicExp(Type, ValueType);

#endregion

/// <summary>
/// The logic expression visitor
/// </summary>
public class LogicExpVisitor : IExpVisitor
{
    public int Priority => EXP_LOGIC_PRIORITY;

    // <inheritdoc/>
    public async Task<SchemaExp?> VisitExpAsync(CompileContext context, SchemaExp exp)
    {
        await Task.Yield();
        if (exp is not FuncCallExp { ApplyMode: ApplyMode.Call, Function: { MethodInfo: { } method } } callExp) return null;
        
        // v in [a, b) - special case
        if (callExp.Function.Name.Equals($"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.between)}", StringComparison.OrdinalIgnoreCase))
        {
            if (callExp.Args.Length < 3)
                throw new FunctionVisitException(ErrorCodes.FUNC_EXP_WRONG_ARGS);
                
            SchemaExp valExp = callExp.Args[0];
            SchemaExp minExp = callExp.Args[1];
            SchemaExp maxExp = callExp.Args[2];
                
            return new BinaryLogicExp(LogicType.AndAlso, 
                (callExp.Args.ElementAtOrDefault(3) as ConstantExp)?.Value.GetValue<bool>() ?? false
                    ? new BinaryLogicExp(LogicType.GreaterEqual, valExp, minExp, exp.ValueType)
                    : new BinaryLogicExp(LogicType.GreaterThan, valExp, minExp, exp.ValueType), 
                (callExp.Args.ElementAtOrDefault(4) as ConstantExp)?.Value.GetValue<bool>() ?? false
                    ? new BinaryLogicExp(LogicType.LessEqual, valExp, maxExp, exp.ValueType)
                    : new BinaryLogicExp(LogicType.LessThan, valExp, maxExp, exp.ValueType), 
                exp.ValueType);
        }
        
        if (method.GetCustomAttribute<LogicAttribute>() is not { } logicAttr)
            return null;

        LogicExp? logicExp = callExp.Function.Args.Length switch
        {
            1 => new UnaryLogicExp(logicAttr.Type, callExp.Args[0], exp.ValueType, logicAttr.IncludeMethod ? callExp.Function : null),
            2 => new BinaryLogicExp(logicAttr.Type, callExp.Args[0], callExp.Args[1], exp.ValueType, logicAttr.IncludeMethod ? callExp.Function : null),
            _ => null
        };

        // Simplify NOT expressions
        if (logicExp?.Type == LogicType.Not && logicExp is UnaryLogicExp { Inner: LogicExp innerExp })
            logicExp = (await NotExpAsync(context.Context, innerExp)) ?? logicExp;
        
        // Re-order the compare expressions
        return ReOrderCompareExp(logicExp);
    }

    // <inheritdoc/>
    public async Task<Expression?> CompileExpAsync(CompileContext context, SchemaExp exp, Type expectedType)
    {
        if (exp is not LogicExp logicExp) return null;

        switch (logicExp)
        {
            // Unary logic
            case UnaryLogicExp unExp:
            {
                switch (unExp.Type)
                {
                    case LogicType.Not:
                        return Expression.Not(await context.CompileSchemaExpAsync(unExp.Inner));
                    case LogicType.IsNull:
                    {
                        Expression innerExp = await context.CompileSchemaExpAsync(unExp.Inner);
                        return Expression.Equal(innerExp, Expression.Constant(null, innerExp.Type));
                    }
                    case LogicType.NotNull:
                    {
                        Expression innerExp = await context.CompileSchemaExpAsync(unExp.Inner);
                        return Expression.NotEqual(innerExp, Expression.Constant(null, innerExp.Type));
                    }
                    case LogicType.IsEmpty:
                    case LogicType.NotEmpty:
                        return await context.CompileSchemaExpAsync(new FuncCallExp(unExp.Method!, [unExp.Inner], unExp.ValueType));
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            // Binary logic
            case BinaryLogicExp binExp:
                Expression left = await context.CompileSchemaExpAsync(binExp.Left);
                Expression right = await context.CompileSchemaExpAsync(binExp.Right);

                return binExp.Type switch
                {
                    LogicType.AndAlso => Expression.AndAlso(left, right),
                    LogicType.OrElse => Expression.OrElse(left, right),
                    LogicType.Equal => Expression.Equal(left, right),
                    LogicType.NotEqual => Expression.NotEqual(left, right),
                    LogicType.GreaterThan => Expression.GreaterThan(left, right),
                    LogicType.GreaterEqual => Expression.GreaterThanOrEqual(left, right),
                    LogicType.LessThan => Expression.LessThan(left, right),
                    LogicType.LessEqual => Expression.LessThanOrEqual(left, right),
                    _ => await context.CompileSchemaExpAsync(new FuncCallExp(binExp.Method!, [binExp.Left, binExp.Right], binExp.ValueType))
                };
        }

        return null;
    }
    
    async Task<LogicExp?> NotExpAsync(SchemaContext context, LogicExp exp)
    {
        return exp switch
        {
            UnaryLogicExp unaryLogicExp => unaryLogicExp.Type switch
            {
                LogicType.Not => unaryLogicExp.Inner as LogicExp,
                
                LogicType.IsNull => new UnaryLogicExp(LogicType.NotNull, unaryLogicExp.Inner, exp.ValueType),
                
                LogicType.IsEmpty => new UnaryLogicExp(LogicType.NotEmpty, unaryLogicExp.Inner, exp.ValueType, await context.GetNodeTypeAsync<FunctionType>($"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.notempty)}") ),
                
                LogicType.NotNull => new UnaryLogicExp(LogicType.IsNull, unaryLogicExp.Inner, exp.ValueType),
                
                LogicType.NotEmpty => new UnaryLogicExp(LogicType.IsEmpty, unaryLogicExp.Inner, exp.ValueType, await context.GetNodeTypeAsync<FunctionType>($"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.isempty)}") ),
                _ => null
            },
            BinaryLogicExp binaryLogicExp => binaryLogicExp.Type switch
            {
                LogicType.AndAlso => new BinaryLogicExp(LogicType.OrElse, 
                    (await NotExpAsync(context, (binaryLogicExp.Left as LogicExp)!))!,
                    (await NotExpAsync(context, (binaryLogicExp.Right as LogicExp)!))!, 
                        exp.ValueType),
                
                LogicType.OrElse => new BinaryLogicExp(LogicType.AndAlso,
                    (await NotExpAsync(context, (binaryLogicExp.Left as LogicExp)!))!,
                    (await NotExpAsync(context, (binaryLogicExp.Right as LogicExp)!))!, 
                        exp.ValueType),
                            
                LogicType.Equal => new BinaryLogicExp(LogicType.NotEqual, binaryLogicExp.Left, binaryLogicExp.Right, exp.ValueType),
                LogicType.NotEqual => new BinaryLogicExp(LogicType.Equal, binaryLogicExp.Left, binaryLogicExp.Right, exp.ValueType),
                LogicType.GreaterThan => new BinaryLogicExp(LogicType.LessEqual, binaryLogicExp.Left, binaryLogicExp.Right, exp.ValueType),
                LogicType.GreaterEqual => new BinaryLogicExp(LogicType.LessThan, binaryLogicExp.Left, binaryLogicExp.Right, exp.ValueType),
                LogicType.LessThan => new BinaryLogicExp(LogicType.GreaterEqual, binaryLogicExp.Left, binaryLogicExp.Right, exp.ValueType),
                LogicType.LessEqual => new BinaryLogicExp(LogicType.GreaterThan, binaryLogicExp.Left, binaryLogicExp.Right, exp.ValueType),
                
                LogicType.Contains => new BinaryLogicExp(LogicType.NotContains, binaryLogicExp.Left, binaryLogicExp.Right, exp.ValueType,
                    await context.GetNodeTypeAsync<FunctionType>($"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.notcontains)}") ),
                
                LogicType.NotContains => new BinaryLogicExp(LogicType.Contains, binaryLogicExp.Left, binaryLogicExp.Right, exp.ValueType,
                    await context.GetNodeTypeAsync<FunctionType>($"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.contains)}") ),
                
                LogicType.StartsWith => new BinaryLogicExp(LogicType.NotStartsWith, binaryLogicExp.Left, binaryLogicExp.Right, exp.ValueType,
                    await context.GetNodeTypeAsync<FunctionType>($"system.str.logic.{nameof(SystemStr.Logic.notstartswith)}") ),
                
                LogicType.NotStartsWith => new BinaryLogicExp(LogicType.StartsWith, binaryLogicExp.Left, binaryLogicExp.Right, exp.ValueType,
                    await context.GetNodeTypeAsync<FunctionType>($"system.str.logic.{nameof(SystemStr.Logic.startswith)}") ),
                
                LogicType.EndsWith => new BinaryLogicExp(LogicType.NotEndsWith, binaryLogicExp.Left, binaryLogicExp.Right, exp.ValueType,
                    await context.GetNodeTypeAsync<FunctionType>($"system.str.logic.{nameof(SystemStr.Logic.notendswith)}") ),
                
                LogicType.NotEndsWith => new BinaryLogicExp(LogicType.EndsWith, binaryLogicExp.Left, binaryLogicExp.Right, exp.ValueType,
                    await context.GetNodeTypeAsync<FunctionType>($"system.str.logic.{nameof(SystemStr.Logic.endswith)}") ),
                
                LogicType.Match => new BinaryLogicExp(LogicType.NotMatch, binaryLogicExp.Left, binaryLogicExp.Right, exp.ValueType,
                    await context.GetNodeTypeAsync<FunctionType>($"system.str.logic.{nameof(SystemStr.Logic.notcontains)}") ),
                
                LogicType.NotMatch => new BinaryLogicExp(LogicType.Match, binaryLogicExp.Left, binaryLogicExp.Right, exp.ValueType,
                    await context.GetNodeTypeAsync<FunctionType>($"system.str.logic.{nameof(SystemStr.Logic.contains)}") ),
                
                _ => null
            },
            _ => null
        };
    }
    
    LogicExp? ReOrderCompareExp(LogicExp? exp)
    {
        if (exp is not BinaryLogicExp binExp) return exp;
        switch (binExp.Type)
        {
            case LogicType.Equal:
            case LogicType.NotEqual:
            case LogicType.GreaterThan:
            case LogicType.GreaterEqual:
            case LogicType.LessThan:
            case LogicType.LessEqual:
                // The field access should be on the left side
                SchemaExp left = binExp.Left;
                SchemaExp right = binExp.Right;
                
                if (right is FieldAccessExp or VariableExp { Value: FieldAccessExp } && 
                    left is not FieldAccessExp && left is not VariableExp { Value: FieldAccessExp })
                {
                    LogicType newType = binExp.Type switch
                    {
                        LogicType.GreaterThan => LogicType.LessThan,
                        LogicType.GreaterEqual => LogicType.LessEqual,
                        LogicType.LessThan => LogicType.GreaterThan,
                        LogicType.LessEqual => LogicType.GreaterEqual,
                        _ => binExp.Type
                    };
                    return new BinaryLogicExp(newType, right, left, exp.ValueType, binExp.Method);
                }
                return binExp;
            default:
                return binExp;
        }
    }
}