using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Function;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;

/// <summary>
/// The field access expression
/// </summary>
/// <param name="Owner">The field owner</param>
/// <param name="FieldName">The field name</param>
/// <param name="SchemeType">The schema type</param>
public record FieldAccessExpression(SchemaExpression Owner, string FieldName, AnySchemeType SchemeType) : SchemaExpression(SchemeType);

/// <summary>
/// The constant expression visitor
/// </summary>
public class FieldAccessExpressionVisitor : IExpressionVisitor
{
    public int Priorty { get; } = EXP_CONSTANT_PRIORITY;

    // <inheritdoc/>
    public SchemaExpression? VisitExpression(SchemaContext context, SchemaExpression exp)
    {
        if (exp is not FuncCallExpression funcCallExp) return null;
        switch (funcCallExp.Function.Name)
        {
            case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.getfield)}":
            {
                if (funcCallExp.Args.Length != 2)
                    throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);
                var ownerExp = funcCallExp.Args[0];
                var fieldNameExp = funcCallExp.Args[1] as ConstantExpression;
                if (fieldNameExp == null || fieldNameExp.Value.ToValue<string>() is not string fieldName || string.IsNullOrEmpty(fieldName))
                    throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);

                return new FieldAccessExpression(ownerExp, fieldName, funcCallExp.SchemeType);
            }
            case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.getfielddefault)}":
            {
                if (funcCallExp.Args.Length != 3)
                    throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);
                var ownerExp = funcCallExp.Args[0];
                var fieldNameExp = funcCallExp.Args[1] as ConstantExpression;
                if (fieldNameExp == null || fieldNameExp.Value.ToValue<string>() is not string fieldName || string.IsNullOrEmpty(fieldName))
                    throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);
                var defaultValueExp = funcCallExp.Args[2] as ConstantExpression;
                if (defaultValueExp == null || defaultValueExp.Value.IsEmpty || !defaultValueExp.SchemaType.CanBeUseAs(exp.SchemaType))
                    throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);

                return new DefaultExpression(new FieldAccessExpression(ownerExp, fieldName, funcCallExp.SchemeType), defaultValueExp.Value);
            }
            default:
                return null;
        }
    }
}