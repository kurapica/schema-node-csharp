using System.Linq.Expressions;
using SchemaNode.Components;
using SchemaNode.Context;

namespace SchemaNode.Runtime;

/// <summary>
/// The reference filter compile context
/// </summary>
public class RefFilterCompileContext(SchemaContext context, FunctionType function) : CompileContext(context, function)
{
    CollectionOperator? _lastDataSourceExp;

    /// <summary>
    /// Transform the last logic expression to filter expression
    /// </summary>
    public override async Task<FunctionTypeSchema> VisitFunctionType()
    {
        if (Function.TryGetRuntimeFuncCache<RefFilterCompileContext, FunctionTypeSchema>(out FunctionTypeSchema? schema))
        {
            _lastDataSourceExp = schema!.Exps.LastOrDefault()?.Value as CollectionOperator;
            return schema;
        }

        schema = await base.VisitFunctionType();
        _lastDataSourceExp = schema.Exps.LastOrDefault()?.Value as CollectionOperator
            ?? throw new FunctionVisitException(Enum.SchemaNodeStatus.FunctionCantBeUsedAsPolicyFilter);
        
        // Re-write the return type to AppSchemaDataFilter
        return Function.SetRuntimeFuncCache<RefFilterCompileContext, FunctionTypeSchema>(
            new FunctionTypeSchema(schema.Args, schema.Exps, typeof(AppSchemaDataFilter)))!;
    }

    /// <summary>
    /// Compile the last logic exp as app schema data filter
    /// </summary>
    public override async Task<Expression> CompileSchemaExpAsync(SchemaExp exp, Type? expectedType = null)
    {
        if (exp != _lastDataSourceExp) return await base.CompileSchemaExpAsync(exp, expectedType);
        
        CollectionRootExp? sourceExp = _lastDataSourceExp;
        Expression? filter = null;
        while (sourceExp != null)
        {
            switch (sourceExp)
            {
                case PredicateCollectionOperator whereExp:
                    filter = filter != null 
                        ? Expression.New(typeof(AppSchemaDataFilterBinary).GetConstructors()[0], Expression.Constant(LogicType.AndAlso), filter,
                            await CompileDataSourceFilter(whereExp.Predicate))
                        : await CompileDataSourceFilter(whereExp.Predicate);
                    sourceExp = whereExp.Root;
                    break;
                case CollectionOperator oper:
                    sourceExp = oper.Root;
                    break;
                default:
                    sourceExp = null;
                    break;
            }
        }

        return filter ?? Expression.New(typeof(AppSchemaDataFilterValue).GetConstructors()[0], Expression.Constant(true));
    }

    async Task<Expression>  CompileDataSourceFilter(SchemaExp exp)
    {
        return exp switch
        {
            FieldAccessExp fieldExp => Expression.New(typeof(AppSchemaDataFilterField).GetConstructors()[0], Expression.Constant(fieldExp.FieldName)),
            UnaryLogicExp unaryExp => Expression.New(typeof(AppSchemaDataFilterUnary).GetConstructors()[0], Expression.Constant(unaryExp.Type), await CompileDataSourceFilter(unaryExp.Inner)),
            BinaryLogicExp binaryExp => Expression.New(typeof(AppSchemaDataFilterBinary).GetConstructors()[0], Expression.Constant(binaryExp.Type), await CompileDataSourceFilter(binaryExp.Left), await CompileDataSourceFilter(binaryExp.Right)),
            _ => Expression.New(typeof(AppSchemaDataFilterValue).GetConstructors()[0], await base.CompileSchemaExpAsync(exp)),
        };
    }
}
