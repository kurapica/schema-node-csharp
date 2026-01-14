using SchemaNode.Context;
using System.Linq.Expressions;
using static SchemaNode.Utility.Constant;
using ExpressionType = SchemaNode.Enum.ExpressionType;

namespace SchemaNode.Runtime;

/// <summary>
/// The policy filter compile context
/// </summary>
public class QueryFilterCompileContext(SchemaContext context, FunctionType function) : CompileContext(context, function)
{
    private LogicExpression? _lastLogicExp;
    
    /// <summary>
    /// The field query access expression
    /// </summary>
    record QueryFieldAccessExpression(string FieldName, AnySchemaType SchemaType) : SchemaExpression(SchemaType);

    /// <summary>
    /// Transform the last logic expression to filter expression
    /// </summary>
    public override async Task<FunctionTypeSchema> VisitFunctionType()
    {
        if (Function.TryGetRuntimeFuncCache<QueryFilterCompileContext, FunctionTypeSchema>(
                out FunctionTypeSchema? schema))
        {
            _lastLogicExp = schema!.Exps.LastOrDefault()?.Value as LogicExpression;
            return schema;
        }
        
        if (Function.Args.Length < 1 || Function.Args[0].SchemaType is not StructType)
            throw new FunctionVisitException(Enum.SchemaNodeStatus.FunctionCantBeUsedAsPolicyFilter, TYPE_FUNC_NOT_VALID_FOR_POLICY_FILTER);
        
        schema = await base.VisitFunctionType();
        _lastLogicExp = schema.Exps.LastOrDefault()?.Value as LogicExpression;
        if (_lastLogicExp == null)
            throw new FunctionVisitException(Enum.SchemaNodeStatus.FunctionCantBeUsedAsPolicyFilter, TYPE_FUNC_NOT_VALID_FOR_POLICY_FILTER);
        
        // Re-write the return type to AppSchemaDataFilter
        return Function.SetRuntimeFuncCache<QueryFilterCompileContext, FunctionTypeSchema>(
            new FunctionTypeSchema(schema.Args.Skip(1).ToArray(), schema.Exps, typeof(AppSchemaDataFilter)))!;
    }

    /// <summary>
    /// Replace the argument field access expression to FieldAccessExpression without owner
    /// </summary>
    public override async Task<SchemaExpression> VisitSchemaExpAsync(SchemaExpression exp)
    {
        if (exp is FuncCallExpression { ExpType: ExpressionType.Call } funcCallExp)
        {
            SchemaExpression[] args = new SchemaExpression[funcCallExp.Args.Length];
            bool changed = false;
            for (int i = 0; i < funcCallExp.Args.Length; i++)
            {
                var oldArg = funcCallExp.Args[i];
                if (oldArg is FieldAccessExpression
                    {
                        Owner: ArgumentExpression { Index: 0 } or VariableExpression { Value: ArgumentExpression { Index: 0 } }
                    } fExp)
                {
                    args[i] = new QueryFieldAccessExpression(fExp.FieldName, fExp.SchemaType);
                    changed = true;
                }
                else
                {
                    args[i] = oldArg;
                }
            }
            if (changed)
                exp = new FuncCallExpression(funcCallExp.Function, args, funcCallExp.SchemaType, funcCallExp.ExpType);
        }
        SchemaExpression result = await base.VisitSchemaExpAsync(exp);
        return (result is FieldAccessExpression { Owner: ArgumentExpression { Index: 0 } or 
            VariableExpression { Value: ArgumentExpression { Index: 0 } } } fieldExp)
            ? new QueryFieldAccessExpression(fieldExp.FieldName, fieldExp.SchemaType)
            : result;
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
            VariableExpression varExp => await CompileDataSourceFilter(varExp.Value), // nested variable
            QueryFieldAccessExpression fieldExp => Expression.New(typeof(AppSchemaDataFilterField).GetConstructors()[0], Expression.Constant(fieldExp.FieldName)),
            UnaryLogicExpression unaryExp => Expression.New(typeof(AppSchemaDataFilterUnary).GetConstructors()[0], Expression.Constant(unaryExp.Type), await CompileDataSourceFilter(unaryExp.Inner)),
            BinaryLogicExpression binaryExp => Expression.New(typeof(AppSchemaDataFilterBinary).GetConstructors()[0], Expression.Constant(binaryExp.Type), await CompileDataSourceFilter(binaryExp.Left), await CompileDataSourceFilter(binaryExp.Right)),
            _ => Expression.New(typeof(AppSchemaDataFilterValue).GetConstructors()[0], await base.CompileSchemaExpAsync(exp)),
        };
    }
}
