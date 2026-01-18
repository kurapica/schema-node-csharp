using SchemaNode.Enum;
using SchemaNode.Function;
using System.Linq.Expressions;
using System.Reflection;
using static SchemaNode.Utility.Constant;
using ExpressionType = SchemaNode.Enum.ExpressionType;

// ReSharper disable NotAccessedPositionalProperty.Global

namespace SchemaNode.Runtime;

#region Logic Exp

/// <summary>
/// The logic exp type
/// </summary>
public enum LogicType
{
    // Combine
    AndAlso,
    OrElse,
    Not,

    // Check
    IsNull,
    IsEmpty,
    NotNull,
    NotEmpty,

    // Compare
    Equal,
    NotEqual,
    GreaterThan,
    GreaterEqual,
    LessThan,
    LessEqual,

    // Collections
    Contains,
    NotContains,

    // String
    StartsWith,
    NotStartsWith,
    EndsWith,
    NotEndsWith,
    Match,
    NotMatch,
}

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
/// <param name="SchemaType">The result type(bool only)</param>
public abstract record LogicExp(LogicType Type, AnySchemaType SchemaType) : SchemaExp(SchemaType);

/// <summary>
/// The unary logic expression
/// </summary>
/// <param name="Type">The logic exp type</param>
/// <param name="Inner">The inner exp</param>
/// <param name="SchemaType">The result type(bool only)</param>
/// <param name="Method">The method if require</param>
public record UnaryLogicExp(LogicType Type, SchemaExp Inner, AnySchemaType SchemaType, MethodInfo? Method = null) : LogicExp(Type, SchemaType);

/// <summary>
/// The binary logic expression
/// </summary>
/// <param name="Type">The logic exp type</param>
/// <param name="Left">The left exp</param>
/// <param name="Right">The right exp</param>
/// <param name="SchemaType">The result type(bool only)</param>
/// <param name="Method">The method if require</param>
public record BinaryLogicExp(LogicType Type, SchemaExp Left, SchemaExp Right, AnySchemaType SchemaType, MethodInfo? Method = null) : LogicExp(Type, SchemaType);

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
        if (exp is not FuncCallExp { ExpType: ExpressionType.Call, Function: { MethodInfo: { } method } } callExp) return null;
        
        // v in [a, b) - special case
        if (callExp.Function.Name.Equals($"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.between)}", StringComparison.OrdinalIgnoreCase))
        {
            if (callExp.Args.Length < 3)
                throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs);
                
            SchemaExp valExp = callExp.Args[0];
            SchemaExp minExp = callExp.Args[1];
            SchemaExp maxExp = callExp.Args[2];
                
            return new BinaryLogicExp(LogicType.AndAlso, 
                (callExp.Args.ElementAtOrDefault(3) as ConstantExp)?.Value.ToValue<bool>() ?? false
                    ? new BinaryLogicExp(LogicType.GreaterEqual, valExp, minExp, exp.SchemaType)
                    : new BinaryLogicExp(LogicType.GreaterThan, valExp, minExp, exp.SchemaType), 
                (callExp.Args.ElementAtOrDefault(4) as ConstantExp)?.Value.ToValue<bool>() ?? false
                    ? new BinaryLogicExp(LogicType.LessEqual, valExp, maxExp, exp.SchemaType)
                    : new BinaryLogicExp(LogicType.LessThan, valExp, maxExp, exp.SchemaType), 
                exp.SchemaType);
        }
        
        if (method.GetCustomAttribute<LogicAttribute>() is not { } logicAttr)
            return null;

        LogicExp? logicExp = callExp.Function.Args.Length switch
        {
            1 => new UnaryLogicExp(logicAttr.Type, callExp.Args[0], exp.SchemaType, logicAttr.IncludeMethod ? method : null),
            2 => new BinaryLogicExp(logicAttr.Type, callExp.Args[0], callExp.Args[1], exp.SchemaType, logicAttr.IncludeMethod ? method : null),
            _ => null
        };

        // Simplify NOT expressions
        if (logicExp?.Type == LogicType.Not && logicExp is UnaryLogicExp { Inner: LogicExp innerExp })
            logicExp = NotExp(innerExp) ?? logicExp;
        
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
                        return Expression.Call(null, unExp.Method!, await context.CompileSchemaExpAsync(unExp.Inner));
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
                    _ => Expression.Call(null, binExp.Method!, left, right)
                };
        }

        return null;
    }
    
    LogicExp? NotExp(LogicExp exp)
    {
        return exp switch
        {
            UnaryLogicExp unaryLogicExp => unaryLogicExp.Type switch
            {
                LogicType.Not => unaryLogicExp.Inner as LogicExp,
                
                LogicType.IsNull => new UnaryLogicExp(LogicType.NotNull, unaryLogicExp.Inner, exp.SchemaType),
                
                LogicType.IsEmpty => new UnaryLogicExp(LogicType.NotEmpty, unaryLogicExp.Inner, exp.SchemaType,
                    typeof(SystemLogic).GetMethod(nameof(SystemLogic.notempty), BindingFlags.Public|BindingFlags.Static)),
                
                LogicType.NotNull => new UnaryLogicExp(LogicType.IsNull, unaryLogicExp.Inner, exp.SchemaType),
                
                LogicType.NotEmpty => new UnaryLogicExp(LogicType.IsEmpty, unaryLogicExp.Inner, exp.SchemaType,
                    typeof(SystemLogic).GetMethod(nameof(SystemLogic.isempty), BindingFlags.Public|BindingFlags.Static)),
                _ => null
            },
            BinaryLogicExp binaryLogicExp => binaryLogicExp.Type switch
            {
                LogicType.AndAlso => new BinaryLogicExp(LogicType.OrElse, 
                    NotExp((binaryLogicExp.Left as LogicExp)!)!,
                    NotExp((binaryLogicExp.Right as LogicExp)!)!, 
                        exp.SchemaType),
                
                LogicType.OrElse => new BinaryLogicExp(LogicType.AndAlso,
                    NotExp((binaryLogicExp.Left as LogicExp)!)!,
                    NotExp((binaryLogicExp.Right as LogicExp)!)!, 
                        exp.SchemaType),
                            
                LogicType.Equal => new BinaryLogicExp(LogicType.NotEqual, binaryLogicExp.Left, binaryLogicExp.Right, exp.SchemaType),
                LogicType.NotEqual => new BinaryLogicExp(LogicType.Equal, binaryLogicExp.Left, binaryLogicExp.Right, exp.SchemaType),
                LogicType.GreaterThan => new BinaryLogicExp(LogicType.LessEqual, binaryLogicExp.Left, binaryLogicExp.Right, exp.SchemaType),
                LogicType.GreaterEqual => new BinaryLogicExp(LogicType.LessThan, binaryLogicExp.Left, binaryLogicExp.Right, exp.SchemaType),
                LogicType.LessThan => new BinaryLogicExp(LogicType.GreaterEqual, binaryLogicExp.Left, binaryLogicExp.Right, exp.SchemaType),
                LogicType.LessEqual => new BinaryLogicExp(LogicType.GreaterThan, binaryLogicExp.Left, binaryLogicExp.Right, exp.SchemaType),
                LogicType.Contains => new BinaryLogicExp(LogicType.NotContains, binaryLogicExp.Left, binaryLogicExp.Right, exp.SchemaType,
                    typeof(SystemCollection).GetMethod(nameof(SystemCollection.notcontains), BindingFlags.Public|BindingFlags.Static)),
                
                LogicType.NotContains => new BinaryLogicExp(LogicType.Contains, binaryLogicExp.Left, binaryLogicExp.Right, exp.SchemaType,
                    typeof(SystemCollection).GetMethod(nameof(SystemCollection.contains), BindingFlags.Public|BindingFlags.Static)),
                
                LogicType.StartsWith => new BinaryLogicExp(LogicType.NotStartsWith, binaryLogicExp.Left, binaryLogicExp.Right, exp.SchemaType,
                    typeof(SystemStr).GetMethod(nameof(SystemStr.notstartswith), BindingFlags.Public|BindingFlags.Static)),
                
                LogicType.NotStartsWith => new BinaryLogicExp(LogicType.StartsWith, binaryLogicExp.Left, binaryLogicExp.Right, exp.SchemaType,
                    typeof(SystemStr).GetMethod(nameof(SystemStr.startswith), BindingFlags.Public|BindingFlags.Static)),
                
                LogicType.EndsWith => new BinaryLogicExp(LogicType.NotEndsWith, binaryLogicExp.Left, binaryLogicExp.Right, exp.SchemaType,
                    typeof(SystemStr).GetMethod(nameof(SystemStr.notendswith), BindingFlags.Public|BindingFlags.Static)),
                
                LogicType.NotEndsWith => new BinaryLogicExp(LogicType.EndsWith, binaryLogicExp.Left, binaryLogicExp.Right, exp.SchemaType,
                    typeof(SystemStr).GetMethod(nameof(SystemStr.endswith), BindingFlags.Public|BindingFlags.Static)),
                
                LogicType.Match => new BinaryLogicExp(LogicType.NotMatch, binaryLogicExp.Left, binaryLogicExp.Right, exp.SchemaType,
                    typeof(SystemStr).GetMethod(nameof(SystemStr.notmatch), BindingFlags.Public|BindingFlags.Static)),
                
                LogicType.NotMatch => new BinaryLogicExp(LogicType.Match, binaryLogicExp.Left, binaryLogicExp.Right, exp.SchemaType,
                    typeof(SystemStr).GetMethod(nameof(SystemStr.match), BindingFlags.Public|BindingFlags.Static)),
                
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
                    return new BinaryLogicExp(newType, right, left, exp.SchemaType, binExp.Method);
                }
                return binExp;
            default:
                return binExp;
        }
    }
}