using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Function;
using static SchemaNode.Utility.Constant;
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
    EndsWith,
    Match,
}

/// <summary>
/// The logic expression
/// </summary>
/// <param name="Type">The logic exp type</param>
/// <param name="SchemaType">The result type(bool only)</param>
public abstract record LogicExpression(LogicExpType Type, AnySchemeType SchemaType) : SchemaExpression(SchemaType);

/// <summary>
/// The unary logic expression
/// </summary>
/// <param name="Type">The logic exp type</param>
/// <param name="Inner">The inner exp</param>
/// <param name="SchemaType">The result type(bool only)</param>
public record UnaryLogicExpression(LogicExpType Type, SchemaExpression Inner, AnySchemeType SchemaType) : LogicExpression(Type, SchemaType);

/// <summary>
/// The binary logic expression
/// </summary>
/// <param name="Type">The logic exp type</param>
/// <param name="Left">The left exp</param>
/// <param name="Right">The right exp</param>
/// <param name="SchemaType">The result type(bool only)</param>
public record BinaryLogicExpression(LogicExpType Type, SchemaExpression Left, SchemaExpression Right, AnySchemeType SchemaType) : LogicExpression(Type, SchemaType);

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
                return new UnaryLogicExpression(LogicExpType.Not, callExp.Args[0], exp.SchemaType);
            
            // isnull(a)
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.isnull)}":
                return new UnaryLogicExpression(LogicExpType.IsNull, callExp.Args[0], exp.SchemaType);
            
            // notnull(a)
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.notnull)}":
                return new UnaryLogicExpression(LogicExpType.NotNull, callExp.Args[0], exp.SchemaType);
            
            // isempty(a)
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.isempty)}":
                return new UnaryLogicExpression(LogicExpType.IsEmpty, callExp.Args[0], exp.SchemaType);
            
            // notempty(a)
            case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.notempty)}":
                return new UnaryLogicExpression(LogicExpType.NotEmpty, callExp.Args[0], exp.SchemaType);
            
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
                return new BinaryLogicExpression(LogicExpType.Contains, callExp.Args[0], callExp.Args[1], exp.SchemaType);
            
            // !a.Contains(b)
            case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.notcontains)}":
                return new BinaryLogicExpression(LogicExpType.NotContains, callExp.Args[0], callExp.Args[1], exp.SchemaType);
            
            // a.StartsWith(b)
            case $"{NS_SYSTEM_STRING}.{nameof(SystemStr.startswith)}":
                return new BinaryLogicExpression(LogicExpType.StartsWith, callExp.Args[0],  callExp.Args[1], exp.SchemaType);
            
            // a.EndsWith(b)
            case $"{NS_SYSTEM_STRING}.{nameof(SystemStr.endswith)}":
                return new BinaryLogicExpression(LogicExpType.EndsWith, callExp.Args[0],  callExp.Args[1], exp.SchemaType);
            
            // a.Match(b)
            case $"{NS_SYSTEM_STRING}.{nameof(SystemStr.match)}":
                return new BinaryLogicExpression(LogicExpType.Match, callExp.Args[0],  callExp.Args[1], exp.SchemaType);
            
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
            case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.fieldendswith)}":
            case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.fieldmatch)}":
            {
                if (callExp.Args.Length != 3)
                    throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);
                
                SchemaExpression ownerExp = callExp.Args[0];
                ConstantExpression? fieldNameExp = callExp.Args[1] as ConstantExpression;
                if (fieldNameExp?.Value.ToValue<string>() is not { } fieldName || string.IsNullOrEmpty(fieldName))
                    throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);

                AnySchemeType? ownerType = ownerExp.SchemaType;
                if (ownerType is ArrayType array) ownerType = array.ElementSchemaType;
                if (ownerType is not StructType @struct)
                    throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);

                AnySchemeType? fieldType = @struct.GetField(fieldName)?.SchemeType;
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
                    $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.fieldendswith)}" => LogicExpType.EndsWith,
                    $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.fieldmatch)}" => LogicExpType.Match,
                    _ => throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID)
                }, new FieldAccessExpression(ownerExp, fieldName, fieldType), callExp.Args[2], callExp.SchemaType);
            }
        }

        return null;
    }
}