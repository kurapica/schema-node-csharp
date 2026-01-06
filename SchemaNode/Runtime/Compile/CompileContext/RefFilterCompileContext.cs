using System.Linq.Expressions;
using SchemaNode.Context;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;

/// <summary>
/// The reference filter compile context
/// </summary>
public class RefFilterCompileContext(SchemaContext context, FunctionType pushFuncType) : CompileContext(context: context, pushFuncType)
{
    DataSourceExpression? _lastDataSourceExp;
    private readonly FunctionType _pushFuncType = pushFuncType;

    /// <summary>
    /// Transform the last logic expression to filter expression
    /// </summary>
    public override async Task<FunctionTypeSchema> VisitFunctionType()
    {
        if (_pushFuncType.TryGetRuntimeFuncCache<RefFilterCompileContext, FunctionTypeSchema>(out FunctionTypeSchema? schema))
        {
            _lastDataSourceExp = schema!.Exps.LastOrDefault()?.Value as DataSourceExpression;
            return schema;
        }

        schema = await base.VisitFunctionType();
        _lastDataSourceExp = schema.Exps.LastOrDefault()?.Value as DataSourceExpression;
        if (_lastDataSourceExp == null)
            throw new FunctionVisitException(Enum.SchemaNodeStatus.FunctionCantBeUsedAsPolicyFilter, TYPE_FUNC_NOT_VALID_FOR_POLICY_FILTER);
        
        // Re-write the return type to AppSchemaDataFilter
        return _pushFuncType.SetRuntimeFuncCache<RefFilterCompileContext, FunctionTypeSchema>(
            new FunctionTypeSchema(schema.Args, schema.Exps, typeof(AppSchemaDataFilter)))!;
    }

    /// <summary>
    /// Compile the last logic exp as app schema data filter
    /// </summary>
    public override async Task<Expression> CompileSchemaExpAsync(SchemaExpression exp, Type? expectedType = null)
    {
        if (exp != _lastDataSourceExp) return await base.CompileSchemaExpAsync(exp, expectedType);
        
        DataSourceExpression? sourceExp = _lastDataSourceExp;
        Expression? filter = null;
        while (sourceExp != null)
        {
            switch (sourceExp)
            {
                case WhereDataSourceExpression whereExp:
                    filter = filter != null 
                        ? Expression.New(typeof(AppSchemaDataFilterBinary).GetConstructors()[0], Expression.Constant(LogicExpType.AndAlso), filter, await CompileDataSourceFilter(whereExp.Filter))
                        : await CompileDataSourceFilter(whereExp.Filter);
                    sourceExp = whereExp.Previous;
                    break;
                case OrderByDataSourceExpression orderByExp:
                    sourceExp = orderByExp.Previous;
                    break;
                case TakeDataSourceExpression takeExp:
                    sourceExp = takeExp.Previous;
                    break;
                case SkipDataSourceExpression skipExp:
                    sourceExp = skipExp.Previous;
                    break;
                default:
                    sourceExp = null;
                    break;
            }
        }

        return filter ?? Expression.New(typeof(AppSchemaDataFilterValue).GetConstructors()[0], Expression.Constant(true));
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
