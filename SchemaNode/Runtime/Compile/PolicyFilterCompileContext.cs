using SchemaNode.Context;
using System.Linq.Expressions;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;

/// <summary>
/// The policy filter compile context
/// </summary>
public class PolicyFilterCompileContext(SchemaContext context, FunctionType funcType) : CompileContext(context, funcType)
{
    LogicExpression? _lastLogicExp = null;

    /// <summary>
    /// Transform the last logic expression to filter expression
    /// </summary>
    public override async Task<FunctionTypeSchema> VisitFunctionType()
    {
        FunctionTypeSchema schema = await base.VisitFunctionType();
        if (schema.Exps.LastOrDefault()?.Value is not LogicExpression logicExp)
            throw new FunctionVisitException(Enum.SchemaNodeStatus.FunctionCantBeUsedAsPolicyFilter, TYPE_FUNC_NOT_VALID_FOR_POLICY_FILTER);
        _lastLogicExp = logicExp;
        return new FunctionTypeSchema(schema.Args, schema.Exps, typeof(AppSchemaDataFilter));
    }

    /// <summary>
    /// Compile the last logic exp as app schema data filter
    /// </summary>
    public override Expression CompileSchemaExpression(SchemaExpression exp, Type? expectedType = null)
    {
        return (exp == _lastLogicExp)
            ? CompileDataSourceFilter(_lastLogicExp)
            : base.CompileSchemaExpression(exp, expectedType);
    }

    Expression CompileDataSourceFilter(SchemaExpression exp)
    {
        return exp switch
        {
            FieldAccessExpression fieldExp => Expression.New(typeof(AppSchemaDataFilterField).GetConstructors()[0], Expression.Constant(fieldExp.FieldName)),
            UnaryLogicExpression unaryExp => Expression.New(typeof(AppSchemaDataFilterUnary).GetConstructors()[0], Expression.Constant(unaryExp.Type), CompileDataSourceFilter(unaryExp.Inner)),
            BinaryLogicExpression binaryExp => Expression.New(typeof(AppSchemaDataFilterBinary).GetConstructors()[0], Expression.Constant(binaryExp.Type), CompileDataSourceFilter(binaryExp.Left), CompileDataSourceFilter(binaryExp.Right)),
            _ => Expression.New(typeof(AppSchemaDataFilterValue).GetConstructors()[0], base.CompileSchemaExpression(exp)),
        };
    }
}
