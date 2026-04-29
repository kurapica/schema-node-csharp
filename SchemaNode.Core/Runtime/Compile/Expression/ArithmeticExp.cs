using System.Linq.Expressions;
using System.Reflection;
using SchemaNode.Enum;
using static SchemaNode.Utility.Constant;
using ExpressionType = SchemaNode.Enum.ExpressionType;

// ReSharper disable NotAccessedPositionalProperty.Global

namespace SchemaNode.Runtime;

#region Arithmetic Exp Types

/// <summary>
/// The Arithmetic exp type
/// </summary>
public enum ArithmeticType
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
    
    // Others
    Transform
}

/// <summary>
/// The Arithmetic exp type
/// </summary>
public class ArithmeticAttribute(ArithmeticType type) : System.Attribute
{
    /// <summary>
    ///  The arithmetic type
    /// </summary>
    public ArithmeticType Type { get; } = type;
}   

/// <summary>
/// The arithmetic expression
/// </summary>
public record ArithmeticExp(ArithmeticType Type, SchemaExp[] Args, NodeType NodeType) : SchemaExp(NodeType);

/// <summary>
/// The transform arithmetic expression
/// </summary>
public record TransformArithmeticExp(MethodInfo Method, SchemaExp[] Args, NodeType NodeType) 
    : ArithmeticExp(ArithmeticType.Transform, Args, NodeType);

#endregion

/// <summary>
/// The Arithmetic expression visitor
/// </summary>
public class ArithmeticExpVisitor : IExpVisitor
{
    // <inheritdoc/>
    public int Priority => EXP_ARITHMETIC_PRIORITY;

    // <inheritdoc/>
    public Task<SchemaExp?> VisitExpAsync(CompileContext context, SchemaExp exp)
    {
        if (exp is not FuncCallExp { ExpType: ExpressionType.Call } callExp ||
            callExp.Function.MethodInfo == null ||
            callExp.Function.MethodInfo.GetCustomAttribute<ArithmeticAttribute>() is not {} attr) 
            return Task.FromResult<SchemaExp?>(null);
        
        return Task.FromResult<SchemaExp?>(
            attr.Type == ArithmeticType.Transform
                ? new TransformArithmeticExp(callExp.Function.MethodInfo, callExp.Args, callExp.NodeType)
                : new ArithmeticExp(attr.Type, callExp.Args, callExp.NodeType));
    }

    // <inheritdoc/>
    public async Task<Expression?> CompileExpAsync(CompileContext context, SchemaExp exp, Type expectedType)
    {
        if (exp is not ArithmeticExp arithmeticExp) return null;
        
        // Prepare argument expressions
        List<Expression> argExps = [];
        foreach (SchemaExp arg in arithmeticExp.Args)
        {
            if (arg is ParamsExp pExp)
            {
                foreach (SchemaExp p in pExp.Exps)
                {
                    argExps.Add(await context.CompileSchemaExpAsync(p));
                }
            }
            else
            {
                argExps.Add(await context.CompileSchemaExpAsync(arg));
            }
        }
        if (argExps.Count == 0) throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs);
        
        // Compile based on arithmetic type
        switch (arithmeticExp.Type)
        {
            case ArithmeticType.Add:
            {
                Expression resultExp = argExps[0];
                for (int i = 1; i < argExps.Count; i++)
                    resultExp = Expression.Add(resultExp, argExps[i]);
                return resultExp;
            }
            case ArithmeticType.Subtract:
            {
                Expression resultExp = argExps[0];
                for (int i = 1; i < argExps.Count; i++)
                    resultExp = Expression.Subtract(resultExp, argExps[i]);
                return resultExp;
            }
            case ArithmeticType.Divide:
            {
                Expression resultExp = argExps[0];
                for (int i = 1; i < argExps.Count; i++)
                    resultExp = Expression.Divide(resultExp, argExps[i]);
                return resultExp;
            }
            case ArithmeticType.Modulo:
            {
                Expression resultExp = argExps[0];
                for (int i = 1; i < argExps.Count; i++)
                    resultExp = Expression.Modulo(resultExp, argExps[i]);
                return resultExp;
            }
            case ArithmeticType.Multiply:
            {
                Expression resultExp = argExps[0];
                for (int i = 1; i < argExps.Count; i++)
                    resultExp = Expression.Multiply(resultExp, argExps[i]);
                return resultExp;
            }
            case ArithmeticType.BitAnd:
            {
                Expression resultExp = argExps[0];
                for (int i = 1; i < argExps.Count; i++)
                    resultExp = Expression.And(resultExp, argExps[i]);
                return resultExp;
            }
            case ArithmeticType.BitOr:
            {
                Expression resultExp = argExps[0];
                for (int i = 1; i < argExps.Count; i++)
                    resultExp = Expression.Or(resultExp, argExps[i]);
                return resultExp;
            }
            case ArithmeticType.BitLeftShift:
            {
                Expression resultExp = argExps[0];
                for (int i = 1; i < argExps.Count; i++)
                    resultExp = Expression.LeftShift(resultExp, argExps[i]);
                return resultExp;
            }
            case ArithmeticType.BitRightShift:
            {
                Expression resultExp = argExps[0];
                for (int i = 1; i < argExps.Count; i++)
                    resultExp = Expression.RightShift(resultExp, argExps[i]);
                return resultExp;
            }
            case ArithmeticType.BitXor:
            {
                Expression resultExp = argExps[0];
                for (int i = 1; i < argExps.Count; i++)
                    resultExp = Expression.ExclusiveOr(resultExp, argExps[i]);
                return resultExp;
            }
            case ArithmeticType.Min:
            {
                Expression resultExp = argExps[0];
                MethodInfo minMethod = typeof(Math).GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .First(m => m.Name == nameof(Math.Min) && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType == resultExp.Type);
                for (int i = 1; i < argExps.Count; i++)
                    resultExp = Expression.Call(null, minMethod, resultExp, argExps[i]);
                return resultExp;
            }
            case ArithmeticType.Max:
            {
                Expression resultExp = argExps[0];
                MethodInfo minMethod = typeof(Math).GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .First(m => m.Name == nameof(Math.Max) && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType == resultExp.Type);
                for (int i = 1; i < argExps.Count; i++)
                    resultExp = Expression.Call(null, minMethod, resultExp, argExps[i]);
                return resultExp;
            }
            case ArithmeticType.BitUnary:
                return Expression.OnesComplement(argExps[0]);
            case ArithmeticType.ToDecimal:
                return Expression.Convert(argExps[0], typeof(decimal));
            case ArithmeticType.ToDouble:
                return Expression.Convert(argExps[0], typeof(double));
            case ArithmeticType.ToSingle:
                return Expression.Convert(argExps[0], typeof(float));
            case ArithmeticType.ToInt:
                return Expression.Convert(argExps[0], typeof(long));
            case ArithmeticType.Transform:
                return Expression.Call(null, ((TransformArithmeticExp)arithmeticExp).Method, argExps);
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}
