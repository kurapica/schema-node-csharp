using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Function;
using System;
using System.Linq.Expressions;
using static SchemaNode.Utility.Constant;
using ExpressionType = SchemaNode.Enum.ExpressionType;

// ReSharper disable NotAccessedPositionalProperty.Global

namespace SchemaNode.Runtime;

/// <summary>
/// The logic exp type
/// </summary>
public enum LogicExpType
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
/// The logic expression
/// </summary>
/// <param name="Type">The logic exp type</param>
/// <param name="SchemaType">The result type(bool only)</param>
public abstract record LogicExpression(LogicExpType Type, AnySchemaType SchemaType) : SchemaExpression(SchemaType);

/// <summary>
/// The unary logic expression
/// </summary>
/// <param name="Type">The logic exp type</param>
/// <param name="Inner">The inner exp</param>
/// <param name="SchemaType">The result type(bool only)</param>
public record UnaryLogicExpression(LogicExpType Type, SchemaExpression Inner, AnySchemaType SchemaType) : LogicExpression(Type, SchemaType);

/// <summary>
/// The unary logic expression with function
/// </summary>
/// <param name="Type">The logic exp type</param>
/// <param name="Function">The function</param>
/// <param name="Inner">The inner exp</param>
/// <param name="SchemaType">The result type(bool only)</param>
public record UnaryLogicFuncExpression(LogicExpType Type, FunctionType Function, SchemaExpression Inner, AnySchemaType SchemaType) : UnaryLogicExpression(Type, Inner, SchemaType);

/// <summary>
/// The binary logic expression
/// </summary>
/// <param name="Type">The logic exp type</param>
/// <param name="Left">The left exp</param>
/// <param name="Right">The right exp</param>
/// <param name="SchemaType">The result type(bool only)</param>
public record BinaryLogicExpression(LogicExpType Type, SchemaExpression Left, SchemaExpression Right, AnySchemaType SchemaType) : LogicExpression(Type, SchemaType);

/// <summary>
/// The binary logic expression with function
/// </summary>
/// <param name="Type">The logic exp type</param>
/// <param name="Function">The function</param>
/// <param name="Left">The left exp</param>
/// <param name="Right">The right exp</param>
/// <param name="SchemaType">The result type(bool only)</param>
public record BinaryLogicFuncExpression(LogicExpType Type, FunctionType Function, SchemaExpression Left, SchemaExpression Right, AnySchemaType SchemaType) : BinaryLogicExpression(Type, Left, Right, SchemaType);

/// <summary>
/// The logic expression visitor
/// </summary>
public class LogicExpressionVisitor : IExpressionVisitor
{
    public int Priority => EXP_LOGIC_PRIORITY;

    // <inheritdoc/>
    public SchemaExpression? VisitExpression(SchemaContext context, SchemaExpression exp)
    {
        if (exp is not FuncCallExpression { ExpType: ExpressionType.Call } callExp) return null;

        // Complex logic expression
        switch (callExp.Function.Name)
        {
            // a && b
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.andalso)}":
                return new BinaryLogicExpression(LogicExpType.AndAlso, callExp.Args[0], callExp.Args[1], exp.SchemaType);
            
            // a || b
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.orelse)}":
                return new BinaryLogicExpression(LogicExpType.OrElse, callExp.Args[0], callExp.Args[1], exp.SchemaType);
            
            // !a
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.not)}":
                return NotExp(context, callExp.Args[0]);
            
            // isnull(a)
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.isnull)}":
                return new UnaryLogicFuncExpression(LogicExpType.IsNull, callExp.Function, callExp.Args[0], exp.SchemaType);
            
            // notnull(a)
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.notnull)}":
                return new UnaryLogicFuncExpression(LogicExpType.NotNull, callExp.Function, callExp.Args[0], exp.SchemaType);
            
            // isempty(a)
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.isempty)}":
                return new UnaryLogicFuncExpression(LogicExpType.IsEmpty, callExp.Function, callExp.Args[0], exp.SchemaType);
            
            // notempty(a)
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.notempty)}":
                return new UnaryLogicFuncExpression(LogicExpType.NotEmpty, callExp.Function, callExp.Args[0], exp.SchemaType);
            
            // a == b
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.equal)}":
                return new BinaryLogicExpression(LogicExpType.Equal, callExp.Args[0], callExp.Args[1], exp.SchemaType);
            
            // a != b
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.notequal)}":
                return new BinaryLogicExpression(LogicExpType.NotEqual, callExp.Args[0], callExp.Args[1], exp.SchemaType);
            
            // a >= b
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.greateequal)}":
                return new BinaryLogicExpression(LogicExpType.GreaterEqual, callExp.Args[0], callExp.Args[1], exp.SchemaType);
            
            // a > b
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.greatethan)}":
                return new BinaryLogicExpression(LogicExpType.GreaterThan, callExp.Args[0], callExp.Args[1], exp.SchemaType);
            
            // a <= b
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.lessequal)}":
                return new BinaryLogicExpression(LogicExpType.LessEqual, callExp.Args[0], callExp.Args[1], exp.SchemaType);
            
            // a < b
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.lessthan)}":
                return new BinaryLogicExpression(LogicExpType.LessThan, callExp.Args[0], callExp.Args[1], exp.SchemaType);
            
            // a.Contains(b)
            case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.contains)}":
                return new BinaryLogicFuncExpression(LogicExpType.Contains, callExp.Function, callExp.Args[0], callExp.Args[1], exp.SchemaType);
            
            // !a.Contains(b)
            case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.notcontains)}":
                return new BinaryLogicFuncExpression(LogicExpType.NotContains, callExp.Function, callExp.Args[0], callExp.Args[1], exp.SchemaType);
            
            // a.StartsWith(b)
            case $"{NS_SYSTEM_STRING}.{nameof(SystemStr.startswith)}":
                return new BinaryLogicFuncExpression(LogicExpType.StartsWith, callExp.Function, callExp.Args[0],  callExp.Args[1], exp.SchemaType);

            // !a.StartsWith(b)
            case $"{NS_SYSTEM_STRING}.{nameof(SystemStr.notstartswith)}":
                return new BinaryLogicFuncExpression(LogicExpType.NotStartsWith, callExp.Function, callExp.Args[0],  callExp.Args[1], exp.SchemaType);

            // a.EndsWith(b)
            case $"{NS_SYSTEM_STRING}.{nameof(SystemStr.endswith)}":
                return new BinaryLogicFuncExpression(LogicExpType.EndsWith, callExp.Function, callExp.Args[0],  callExp.Args[1], exp.SchemaType);

            // !a.EndsWith(b)
            case $"{NS_SYSTEM_STRING}.{nameof(SystemStr.notendswith)}":
                return new BinaryLogicFuncExpression(LogicExpType.NotEndsWith, callExp.Function, callExp.Args[0],  callExp.Args[1], exp.SchemaType);

            // a.Match(b)
            case $"{NS_SYSTEM_STRING}.{nameof(SystemStr.match)}":
                return new BinaryLogicFuncExpression(LogicExpType.Match, callExp.Function, callExp.Args[0],  callExp.Args[1], exp.SchemaType);

            // !a.Match(b)
            case $"{NS_SYSTEM_STRING}.{nameof(SystemStr.notmatch)}":
                return new BinaryLogicFuncExpression(LogicExpType.NotMatch, callExp.Function, callExp.Args[0],  callExp.Args[1], exp.SchemaType);

            // a in [b, c)
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.between)}":
            {
                if (callExp.Args.Length < 3)
                    throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);
                
                SchemaExpression vexp = callExp.Args[0];
                SchemaExpression minExp = callExp.Args[1];
                SchemaExpression maxExp = callExp.Args[2];
                
                return new BinaryLogicExpression(LogicExpType.AndAlso, 
                    (callExp.Args.ElementAtOrDefault(3) as ConstantExpression)?.Value.ToValue<bool>() ?? false
                        ? new BinaryLogicExpression(LogicExpType.GreaterEqual, vexp, minExp, exp.SchemaType)
                        : new BinaryLogicExpression(LogicExpType.GreaterThan, vexp, minExp, exp.SchemaType), 
                    (callExp.Args.ElementAtOrDefault(4) as ConstantExpression)?.Value.ToValue<bool>() ?? false
                        ? new BinaryLogicExpression(LogicExpType.LessEqual, vexp, maxExp, exp.SchemaType)
                        : new BinaryLogicExpression(LogicExpType.LessThan, vexp, maxExp, exp.SchemaType), 
                    exp.SchemaType);
            }

            // field compare
            case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.fieldequal)}":
            case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.fieldnotequal)}":
            case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.fieldgreateequal)}":
            case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.fieldgreatethan)}":
            case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.fieldlessequal)}":
            case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.fieldlessthan)}":
            case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.fieldstartswith)}":
            case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.fieldnotstartswith)}":
            case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.fieldendswith)}":
            case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.fieldnotendswith)}":
            case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.fieldmatch)}":
            case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.fieldnotmatch)}":
            {
                if (callExp.Args.Length != 3)
                    throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);
                
                SchemaExpression ownerExp = callExp.Args[0];
                ConstantExpression? fieldNameExp = callExp.Args[1] as ConstantExpression;
                if (fieldNameExp?.Value.ToValue<string>() is not { } fieldName || string.IsNullOrEmpty(fieldName))
                    throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);

                AnySchemaType? ownerType = ownerExp.SchemaType;
                if (ownerType is ArrayType array) ownerType = array.ElementSchemaType;
                if (ownerType is not StructType @struct)
                    throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);

                AnySchemaType? fieldType = @struct.GetField(fieldName)?.SchemeType;
                if (fieldType == null)
                    throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);

                return new BinaryLogicExpression(callExp.Function.Name switch
                {
                    $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.fieldequal)}" => LogicExpType.Equal,
                    $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.fieldnotequal)}" => LogicExpType.NotEqual,
                    $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.fieldgreateequal)}" => LogicExpType.GreaterEqual,
                    $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.fieldgreatethan)}" => LogicExpType.GreaterThan,
                    $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.fieldlessequal)}" => LogicExpType.LessEqual,
                    $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.fieldlessthan)}" => LogicExpType.LessThan,
                    $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.fieldstartswith)}" => LogicExpType.StartsWith,
                    $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.fieldnotstartswith)}" => LogicExpType.NotStartsWith,
                    $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.fieldendswith)}" => LogicExpType.EndsWith,
                    $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.fieldnotendswith)}" => LogicExpType.NotEndsWith,
                    $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.fieldmatch)}" => LogicExpType.Match,
                    $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.fieldnotmatch)}" => LogicExpType.NotMatch,
                    _ => throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID)
                }, new FieldAccessExpression(ownerExp, fieldName, fieldType), callExp.Args[2], callExp.SchemaType);
            }
        }

        return null;
    }

    // <inheritdoc/>
    public Expression? CompileExpression(CompileContext context, SchemaExpression exp)
    {
        if (exp is not LogicExpression logicExp) return null;

        switch (logicExp)
        {
            // Unary logic with function
            case UnaryLogicFuncExpression unFuncExp:
                return context.CompileSchemaExpression(new FuncCallExpression(unFuncExp.Function, [unFuncExp.Inner], unFuncExp.SchemaType));
            
            // Binary logic with function
            case BinaryLogicFuncExpression binFuncExp:
                return context.CompileSchemaExpression(new FuncCallExpression(binFuncExp.Function, [binFuncExp.Left, binFuncExp.Right], binFuncExp.SchemaType));
            
            // Unary logic
            case UnaryLogicExpression unExp:
                switch (unExp.Type)
                {
                    case LogicExpType.Not:
                        return Expression.Not(context.CompileSchemaExpression(unExp.Inner));
                }
                break;
            
            // Binary logic
            case BinaryLogicExpression binExp:
                switch (binExp.Type)
                {
                    case LogicExpType.AndAlso:
                        return Expression.AndAlso(context.CompileSchemaExpression(binExp.Left), context.CompileSchemaExpression(binExp.Right));
                    
                    case LogicExpType.OrElse:
                        return Expression.OrElse(context.CompileSchemaExpression(binExp.Left), context.CompileSchemaExpression(binExp.Right));
                    
                    case LogicExpType.Equal:
                        return Expression.Equal(context.CompileSchemaExpression(binExp.Left), context.CompileSchemaExpression(binExp.Right));
                        
                    case LogicExpType.NotEqual:
                        return Expression.NotEqual(context.CompileSchemaExpression(binExp.Left), context.CompileSchemaExpression(binExp.Right));
                        
                    case LogicExpType.GreaterThan:
                        return Expression.GreaterThan(context.CompileSchemaExpression(binExp.Left), context.CompileSchemaExpression(binExp.Right));
                        
                    case LogicExpType.GreaterEqual:
                        return Expression.GreaterThanOrEqual(context.CompileSchemaExpression(binExp.Left), context.CompileSchemaExpression(binExp.Right));
                        
                    case LogicExpType.LessThan:
                        return Expression.LessThan(context.CompileSchemaExpression(binExp.Left), context.CompileSchemaExpression(binExp.Right));
                    
                    case LogicExpType.LessEqual:
                        return Expression.LessThanOrEqual(context.CompileSchemaExpression(binExp.Left), context.CompileSchemaExpression(binExp.Right));
                }
                break;
        }

        return null;
    }

    SchemaExpression NotExp(SchemaContext context, SchemaExpression exp)
    {
        switch (exp)
        {
            case UnaryLogicFuncExpression unaryFuncExp:
                switch (unaryFuncExp.Type)
                {
                    case LogicExpType.IsNull:
                        return new UnaryLogicFuncExpression(LogicExpType.NotNull, context.GetSchemaType<FunctionType>($"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.notnull)}")!, unaryFuncExp.Inner, unaryFuncExp.SchemaType);
                    case LogicExpType.NotNull:
                        return new UnaryLogicFuncExpression(LogicExpType.IsNull, context.GetSchemaType<FunctionType>($"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.isnull)}")!, unaryFuncExp.Inner, unaryFuncExp.SchemaType);
                    case LogicExpType.IsEmpty:
                        return new UnaryLogicFuncExpression(LogicExpType.NotEmpty, context.GetSchemaType<FunctionType>($"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.notempty)}")!, unaryFuncExp.Inner, unaryFuncExp.SchemaType);
                    case LogicExpType.NotEmpty:
                        return new UnaryLogicFuncExpression(LogicExpType.IsEmpty, context.GetSchemaType<FunctionType>($"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.isempty)}")!, unaryFuncExp.Inner, unaryFuncExp.SchemaType);
                }
                break;

            case UnaryLogicExpression unaryExp:
                switch (unaryExp.Type)
                {
                    case LogicExpType.Not:
                        return unaryExp.Inner;
                }
                break;

            case BinaryLogicFuncExpression binFuncExp:
                switch (binFuncExp.Type)
                {
                    case LogicExpType.Contains:
                        return new BinaryLogicFuncExpression(LogicExpType.NotContains, context.GetSchemaType<FunctionType>($"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.notcontains)}")!, binFuncExp.Left, binFuncExp.Right, binFuncExp.SchemaType);
                    case LogicExpType.NotContains:
                        return new BinaryLogicFuncExpression(LogicExpType.Contains, context.GetSchemaType<FunctionType>($"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.contains)}")!, binFuncExp.Left, binFuncExp.Right, binFuncExp.SchemaType);
                    case LogicExpType.StartsWith:
                        return new BinaryLogicFuncExpression(LogicExpType.NotStartsWith, context.GetSchemaType<FunctionType>($"{NS_SYSTEM_STRING}.{nameof(SystemStr.notstartswith)}")!, binFuncExp.Left, binFuncExp.Right, binFuncExp.SchemaType);
                    case LogicExpType.NotStartsWith:
                        return new BinaryLogicFuncExpression(LogicExpType.StartsWith, context.GetSchemaType<FunctionType>($"{NS_SYSTEM_STRING}.{nameof(SystemStr.startswith)}")!, binFuncExp.Left, binFuncExp.Right, binFuncExp.SchemaType);
                    case LogicExpType.EndsWith:
                        return new BinaryLogicFuncExpression(LogicExpType.NotEndsWith, context.GetSchemaType<FunctionType>($"{NS_SYSTEM_STRING}.{nameof(SystemStr.notendswith)}")!, binFuncExp.Left, binFuncExp.Right, binFuncExp.SchemaType);
                    case LogicExpType.NotEndsWith:
                        return new BinaryLogicFuncExpression(LogicExpType.EndsWith, context.GetSchemaType<FunctionType>($"{NS_SYSTEM_STRING}.{nameof(SystemStr.endswith)}")!, binFuncExp.Left, binFuncExp.Right, binFuncExp.SchemaType);
                    case LogicExpType.Match:
                        return new BinaryLogicFuncExpression(LogicExpType.NotMatch, context.GetSchemaType<FunctionType>($"{NS_SYSTEM_STRING}.{nameof(SystemStr.notmatch)}")!, binFuncExp.Left, binFuncExp.Right, binFuncExp.SchemaType);
                    case LogicExpType.NotMatch:
                        return new BinaryLogicFuncExpression(LogicExpType.Match, context.GetSchemaType<FunctionType>($"{NS_SYSTEM_STRING}.{nameof(SystemStr.match)}")!, binFuncExp.Left, binFuncExp.Right, binFuncExp.SchemaType);

                }
                break;

            case BinaryLogicExpression binaryExp:
                switch (binaryExp.Type)
                {
                    case LogicExpType.AndAlso:
                        return new BinaryLogicExpression(LogicExpType.OrElse,
                            NotExp(context, binaryExp.Left),
                            NotExp(context, binaryExp.Right),
                            binaryExp.SchemaType);
                    case LogicExpType.OrElse:
                        return new BinaryLogicExpression(LogicExpType.AndAlso,
                            NotExp(context, binaryExp.Left),
                            NotExp(context, binaryExp.Right),
                            binaryExp.SchemaType);
                    case LogicExpType.Equal:
                        return new BinaryLogicExpression(LogicExpType.NotEqual, binaryExp.Left, binaryExp.Right, binaryExp.SchemaType);
                    case LogicExpType.NotEqual:
                        return new BinaryLogicExpression(LogicExpType.Equal, binaryExp.Left, binaryExp.Right, binaryExp.SchemaType);
                    case LogicExpType.GreaterThan:
                        return new BinaryLogicExpression(LogicExpType.LessEqual, binaryExp.Left, binaryExp.Right, binaryExp.SchemaType);
                    case LogicExpType.GreaterEqual:
                        return new BinaryLogicExpression(LogicExpType.LessThan, binaryExp.Left, binaryExp.Right, binaryExp.SchemaType);
                    case LogicExpType.LessThan:
                        return new BinaryLogicExpression(LogicExpType.GreaterEqual, binaryExp.Left, binaryExp.Right, binaryExp.SchemaType);
                    case LogicExpType.LessEqual:
                        return new BinaryLogicExpression(LogicExpType.GreaterThan, binaryExp.Left, binaryExp.Right, binaryExp.SchemaType);

                }
                break;
        }
        return new UnaryLogicExpression(LogicExpType.Not, exp, exp.SchemaType);
    }
}