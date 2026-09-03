using SchemaNode.Context;
using System.Linq.Expressions;
using SchemaNode.Data;
using SchemaNode.Enum;
using SchemaNode.Property.Common;
using SchemaNode.Relation;
using SchemaNode.Schema;
using SchemaNode.Utility;

namespace SchemaNode.Runtime;

/// <summary>
/// The policy filter compile context
/// </summary>
public class QueryFilterCompileContext : CompileContext
{
    private LogicExp? _lastLogicExp;
    private StructType? _queryType;

    /// <summary>
    /// The policy filter compile context
    /// </summary>
    public QueryFilterCompileContext(SchemaContext context, FunctionType function) : base(context, function)
    {
        if (Function.Args.Length < 1 || Function.Args[0].ValueType is not StructType structType)
            throw new FunctionVisitException(AppErrorCodes.FUNC_IS_NOT_POLICY_FILTER);
        _queryType = structType;
    }

    /// <summary>
    /// The field query access expression
    /// </summary>
    record QueryFieldAccessExpression(string FieldName, IValueTypeAccess ValueType) : SchemaExp(ValueType);

    /// <summary>
    /// Transform the last logic expression to filter expression
    /// </summary>
    public override async Task<FunctionTypeSchema> VisitFunctionType()
    {
        if (Function.TryGetRuntimeFuncCache<QueryFilterCompileContext, FunctionTypeSchema>(out FunctionTypeSchema? schema))
        {
            _lastLogicExp = schema!.Exps.LastOrDefault()?.Value as LogicExp;
            if (_lastLogicExp == null)
                throw new FunctionVisitException(AppErrorCodes.FUNC_IS_NOT_POLICY_FILTER);
            return schema;
        }
        
        schema = await base.VisitFunctionType();
        _lastLogicExp = schema.Exps.LastOrDefault()?.Value as LogicExp;
        if (_lastLogicExp == null)
            throw new FunctionVisitException(AppErrorCodes.FUNC_IS_NOT_POLICY_FILTER);
        
        // Re-write the return type to AppSchemaDataFilter
        return Function.SetRuntimeFuncCache<QueryFilterCompileContext, FunctionTypeSchema>(
            new FunctionTypeSchema(schema.Args.Skip(1).ToArray(), schema.Exps, typeof(AppSchemaDataFilter)))!;
    }

    /// <summary>
    /// Replace the argument field access expression to FieldAccessExpression without owner
    /// </summary>
    public override async Task<SchemaExp> VisitSchemaExpAsync(SchemaExp exp)
    {
        if (exp is FuncCallExp { ApplyMode: ApplyMode.Call } funcCallExp)
        {
            SchemaExp[] args = new SchemaExp[funcCallExp.Args.Length];
            bool changed = false;
            for (int i = 0; i < funcCallExp.Args.Length; i++)
            {
                var oldArg = funcCallExp.Args[i];
                var innerArg = oldArg is DefaultExp dftExp ? dftExp.Inner : oldArg;
                if (innerArg is FieldAccessExp
                    {
                        Owner: ArgumentExp { Index: 0 } or VariableExp { Value: ArgumentExp { Index: 0 } }
                    } fExp)
                {
                    var field = _queryType!.GetField(fExp.FieldName);
                    if (field == null) throw new FunctionVisitException(AppErrorCodes.FUNC_IS_NOT_POLICY_FILTER);

                    // Check field source
                    if (field.DisplayOnly == true)
                    {
                        var relation = _queryType.GetRelations(fExp.FieldName)
                            .FirstOrDefault(r => r.Process is CallProcess && r.ForProperty<Default>());
                        var call = (relation?.Process as CallProcess)!;
                        if (call.FuncType == null) throw new FunctionVisitException(AppErrorCodes.FUNC_IS_NOT_POLICY_FILTER);

                        if (DynamicTableSchema.IsReferenceFunc(call.Func))
                        {
                            // From third app field, leave it to data source to handle
                            args[i] = new QueryFieldAccessExpression(fExp.FieldName, fExp.ValueType);
                        }
                        else
                        {
                            SchemaExp[] replaceArgs = new SchemaExp[call.Args.Length];
                            for (int j = 0; j < call.Args.Length; j++)
                            {
                                CallArg a = call.Args[j];
                                if (!string.IsNullOrWhiteSpace(a.Source))
                                {
                                    var fld = _queryType.GetField(a.Source);
                                    if (fld == null)
                                        throw new FunctionVisitException(AppErrorCodes.FUNC_IS_NOT_POLICY_FILTER);
                                    replaceArgs[j] = new FieldAccessExp(fExp.Owner, fld.Name, fld.Type!);
                                }
                                else
                                {
                                    var valueNode = await Context.GetSchemaNodeAsync(a.Value, a.ValueType ?? call.FuncType.Args[j].ValueType, true)
                                        ?? throw new FunctionVisitException(AppErrorCodes.FUNC_IS_NOT_POLICY_FILTER);
                                    replaceArgs[j] = new ConstantExp(valueNode);
                                }
                            }

                            var replaceExp = new FuncCallExp(call.FuncType, replaceArgs, field.Type!);
                            args[i] = await VisitSchemaExpAsync(replaceExp);
                        }
                    }
                    else
                    {
                        args[i] = new QueryFieldAccessExpression(fExp.FieldName, fExp.ValueType);
                    }

                    changed = true;
                }
                else
                {
                    args[i] = oldArg;
                }
            }
            if (changed)
                exp = new FuncCallExp(funcCallExp.Function, args, funcCallExp.ValueType, funcCallExp.ApplyMode);
        }
        SchemaExp result = await base.VisitSchemaExpAsync(exp);
        return (result is FieldAccessExp { Owner: ArgumentExp { Index: 0 } or 
            VariableExp { Value: ArgumentExp { Index: 0 } } } fieldExp)
            ? new QueryFieldAccessExpression(fieldExp.FieldName, fieldExp.ValueType)
            : result;
    }

    /// <summary>
    /// Compile the last logic exp as app schema data filter
    /// </summary>
    public override Task<Expression> CompileSchemaExpAsync(SchemaExp exp, Type? expectedType = null)
    {
        return ReferenceEquals(exp, _lastLogicExp)
            ? CompileDataSourceFilter(_lastLogicExp)
            : base.CompileSchemaExpAsync(exp, expectedType);
    }

    async Task<Expression>  CompileDataSourceFilter(SchemaExp exp)
    {
        return exp switch
        {
            VariableExp varExp => await CompileDataSourceFilter(varExp.Value), // nested variable
            DefaultExp defaultExp => await CompileDataSourceFilter(defaultExp.Inner),
            QueryFieldAccessExpression fieldExp => Expression.New(typeof(AppSchemaDataFilterField).GetConstructors()[0], Expression.Constant(fieldExp.FieldName)),
            UnaryLogicExp unaryExp => Expression.New(typeof(AppSchemaDataFilterUnary).GetConstructors()[0], Expression.Constant(unaryExp.Type), await CompileDataSourceFilter(unaryExp.Inner)),
            BinaryLogicExp binaryExp => Expression.New(typeof(AppSchemaDataFilterBinary).GetConstructors()[0], Expression.Constant(binaryExp.Type), await CompileDataSourceFilter(binaryExp.Left), await CompileDataSourceFilter(binaryExp.Right)),
            ArithmeticExp arExp => Expression.New(typeof(AppSchemaDataFilterArith).GetConstructors()[0], Expression.Constant(arExp.Type), arExp.Args.Length > 2 ? await CompileDataSourceFilter(new ArithmeticExp(arExp.Type, arExp.Args.SkipLast(1).ToArray(), arExp.ValueType)) : await CompileDataSourceFilter(arExp.Args[0]), await CompileDataSourceFilter(arExp.Args.Last())),
            _ => Expression.New(typeof(AppSchemaDataFilterValue).GetConstructors()[0], Expression.Convert(await base.CompileSchemaExpAsync(exp), typeof(object))),
        };
    }
}
