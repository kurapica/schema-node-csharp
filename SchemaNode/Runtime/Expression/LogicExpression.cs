using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Function;
using System.Reflection;
using static SchemaNode.Utility.Constant;

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

    // Check
    IsNull,
    IsEmpty,

    NotNull,
    NotEmpty,

    // Complex: For analyze use only
    Complex, 

    // String
    StartsWith,
    EndsWith,
    Match
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
/// The binary expression attribute
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public class LogicExpAttribute(LogicExpType type = LogicExpType.Complex) : System.Attribute
{
    /// <summary>
    /// The logic expression type
    /// </summary>
    public LogicExpType Type { get; } = type;
}

/// <summary>
/// The logic expression visitor
/// </summary>
public class LogicExpressionVisitor : IExpressionVisitor
{
    public int Priorty => EXP_LOGIC_PRIORITY;

    // <inheritdoc/>
    public SchemaExpression? VisitExpression(SchemaContext context, SchemaExpression exp)
    {
        if (exp is not FuncCallExpression callExp) return null;

        // For simple logic expression
        var attr = callExp.Function?.FuncInfo?.Method?.GetCustomAttribute<LogicExpAttribute>();
        if (attr == null) return null;

        // Complex logic expression
        if (attr.Type == LogicExpType.Complex)
        {
            switch (callExp.Function?.Name)
            {
                // a in [b, c)
                case $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.between)}":
                {
                    if (callExp.Args.Length >= 3)
                    {
                        var vexp = callExp.Args[0];
                        var minExp = callExp.Args[1];
                        var maxExp = callExp.Args[2];
                        var includeMin = (callExp.Args.ElementAtOrDefault(3) as ConstantExpression)?.Value.ToValue<bool>() ?? false;
                        var includeMax = (callExp.Args.ElementAtOrDefault(4) as ConstantExpression)?.Value.ToValue<bool>() ?? false;

                        var leftExp = includeMin
                            ? new BinaryLogicExpression(LogicExpType.GreaterEqual, vexp, minExp, exp.SchemaType)
                            : new BinaryLogicExpression(LogicExpType.GreaterThan, vexp, minExp, exp.SchemaType);
                        var rightExp = includeMax
                            ? new BinaryLogicExpression(LogicExpType.LessEqual, vexp, maxExp, exp.SchemaType)
                            : new BinaryLogicExpression(LogicExpType.LessThan, vexp, maxExp, exp.SchemaType);
                        return new BinaryLogicExpression(LogicExpType.AndAlso, leftExp, rightExp, exp.SchemaType);
                    }
                    throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);
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
                    var ownerExp = callExp.Args[0];
                    var fieldNameExp = callExp.Args[1] as ConstantExpression;
                    if (fieldNameExp == null || fieldNameExp.Value.ToValue<string>() is not string fieldName || string.IsNullOrEmpty(fieldName))
                        throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);

                    var ownerType = ownerExp.SchemaType;
                    if (ownerType is ArrayType array) ownerType = array.ElementSchemaType;
                    if (ownerType is not StructType @struct)
                        throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);

                    var fieldType = @struct.GetField(fieldName)?.SchemeType;
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

                default:
                    return null;
            }
        }

        return callExp.Args.Length == 1
            ? new UnaryLogicExpression(attr.Type, callExp.Args[0], callExp.SchemeType)
            : new BinaryLogicExpression(attr.Type, callExp.Args[0], callExp.Args[1], callExp.SchemeType);
    }
}