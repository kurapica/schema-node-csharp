using SchemaNode.Context;
using System.Linq.Expressions;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;

/// <summary>
/// The policy filter compile context
/// </summary>
public class PolicyFilterCompileContext(SchemaContext context, FunctionType function) : CompileContext(context, function)
{
    LogicExpression? _lastLogicExp;

    /// <summary>
    /// Transform the last logic expression to filter expression
    /// </summary>
    public override async Task<FunctionTypeSchema> VisitFunctionType()
    {
        if (Function.TryGetRuntimeFuncCache<PolicyFilterCompileContext, FunctionTypeSchema>(
                out FunctionTypeSchema? schema))
        {
            _lastLogicExp = schema!.Exps.LastOrDefault()?.Value as LogicExpression;
            return schema;
        }

        schema = await base.VisitFunctionType();
        _lastLogicExp = schema.Exps.LastOrDefault()?.Value as LogicExpression;
        if (_lastLogicExp == null)
            throw new FunctionVisitException(Enum.SchemaNodeStatus.FunctionCantBeUsedAsPolicyFilter, TYPE_FUNC_NOT_VALID_FOR_POLICY_FILTER);
        
        // Re-write the return type to AppSchemaDataFilter
        return Function.SetRuntimeFuncCache<PolicyFilterCompileContext, FunctionTypeSchema>(
            new FunctionTypeSchema(schema.Args, schema.Exps, typeof(AppSchemaDataFilter)))!;
    }

    /// <summary>
    /// Compile the last logic exp as app schema data filter
    /// </summary>
    public override Task<Expression> CompileSchemaExpAsync(SchemaExpression exp, Type? expectedType = null)
    {
        return (exp == _lastLogicExp)
            ? CompileDataSourceFilter(_lastLogicExp)
            : base.CompileSchemaExpAsync(exp, expectedType);
    }

    async Task<Expression>  CompileDataSourceFilter(SchemaExpression exp)
    {
        return exp switch
        {
            FieldAccessExpression fieldExp => Expression.New(typeof(AppSchemaDataFilterField).GetConstructors()[0], Expression.Constant(fieldExp.FieldName)),
            UnaryLogicExpression unaryExp => Expression.New(typeof(AppSchemaDataFilterUnary).GetConstructors()[0], Expression.Constant(unaryExp.Type), await CompileDataSourceFilter(unaryExp.Inner)),
            BinaryLogicExpression binaryExp => Expression.New(typeof(AppSchemaDataFilterBinary).GetConstructors()[0], Expression.Constant(binaryExp.Type), await CompileDataSourceFilter(binaryExp.Left), await CompileDataSourceFilter(binaryExp.Right)),
            _ => Expression.New(typeof(AppSchemaDataFilterValue).GetConstructors()[0], await base.CompileSchemaExpAsync(exp)),
        };
    }
}
