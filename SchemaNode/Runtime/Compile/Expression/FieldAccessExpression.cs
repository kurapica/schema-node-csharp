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
/// <param name="SchemaType">The schema type</param>
public record FieldAccessExpression(SchemaExpression Owner, string FieldName, AnySchemaType SchemaType) : SchemaExpression(SchemaType);

/// <summary>
/// The constant expression visitor
/// </summary>
public class FieldAccessExpressionVisitor : IExpressionVisitor
{
    // <inheritdoc/>
    public int Priority => EXP_CONSTANT_PRIORITY;

    // <inheritdoc/>
    public SchemaExpression? VisitExpression(SchemaContext context, SchemaExpression exp)
    {
        if (exp is not FuncCallExpression { ExpType: ExpressionType.Call } callExp) return null;
        
        switch (callExp.Function.Name)
        {
            // a[b]
            case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.getfield)}":
            {
                if (callExp.Args.Length != 2 || 
                    (callExp.Args[1] as ConstantExpression)?.Value.ToValue<string>() is not { } fieldName || 
                    string.IsNullOrEmpty(fieldName))
                    throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);

                return new FieldAccessExpression(callExp.Args[0], fieldName, callExp.SchemaType);
            }
            
            // a[b] ?? defaultValue
            case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.getfielddefault)}":
            {
                if (callExp.Args.Length != 3 || 
                    (callExp.Args[1] as ConstantExpression)?.Value.ToValue<string>() is not { } fieldName || 
                    string.IsNullOrEmpty(fieldName) || 
                    callExp.Args[2] is not ConstantExpression defaultValueExp || 
                    defaultValueExp.Value.IsEmpty || 
                    !defaultValueExp.SchemaType.CanBeUseAs(exp.SchemaType))
                    throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);

                return new DefaultExpression(new FieldAccessExpression(callExp.Args[0], fieldName, callExp.SchemaType), defaultValueExp.Value);
            }
        }

        return null;
    }
}