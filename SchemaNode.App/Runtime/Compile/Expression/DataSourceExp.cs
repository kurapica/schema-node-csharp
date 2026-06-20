using SchemaNode.Enum;
using SchemaNode.Function;
using System.Linq.Expressions;
using System.Reflection;
using SchemaNode.Context;
using SchemaNode.Data;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

// ReSharper disable NotAccessedPositionalProperty.Global

namespace SchemaNode.Runtime;

#region Data Source Exp

/// <summary>
/// The data source
/// </summary>
public record DataSource(string App, string Field, ValueType ValueType);

/// <summary>
/// The data source expression
/// </summary>
public record DataSourceExp(DataSource Source) : SchemaExp(Source.ValueType);

#endregion

/// <summary>
/// The data source visitor
/// </summary>
public class DataSourceExpVisitor : IExpVisitor
{
    /// <inheritdoc />
    public int Priority => EXP_DATA_SOURCE_PRIORITY;

    /// <inheritdoc />
    public async Task<SchemaExp?> VisitExpAsync(CompileContext context, SchemaExp exp)
    {
        if (exp is not FuncCallExp { ExpType: ExpType.Call } callExp) return null;

        // Data source check
        if (callExp.Function.Name != $"{NS_SYSTEM_DATA}.{nameof(SystemAppData.getdatasource)}") return null;
        string? app = callExp.Args.ElementAtOrDefault(0) is ConstantExp appExp ? appExp.Value.GetValue<string>() :  null;
        string? field = callExp.Args.ElementAtOrDefault(1) is ConstantExp fldExp ? fldExp.Value.GetValue<string>() :  null;

        // App & Field must be provided
        if (string.IsNullOrEmpty(app) || string.IsNullOrEmpty(field))
            throw new FunctionVisitException(ErrorCodes.FUNC_EXP_WRONG_ARGS);

        // App & Field must be valid
        var appType = await context.Context.GetAppTypeAsync(app);
        AppFieldType? appField = appType?.GetField(field);
        ValueType? schemaType = appField?.ValueType;
        if (schemaType == null && !string.IsNullOrEmpty(appField?.Type))
            schemaType = await context.Context.GetNodeTypeAsync<ValueType>(appField.Type);
        return schemaType is ArrayType { Element: StructType, Primary: { Count: > 0 } }
            ? new DataSourceExp(new DataSource(app, field, schemaType))
            : null; // call directly
    }

    /// <inheritdoc />
    public async Task<Expression?> CompileExpAsync(CompileContext context, SchemaExp exp, Type expectedType)
    {
        #region Search DataSource
        
        DataSourceExp? dataSource = null;
        SchemaExp? root = exp;
        while (root != null)
        {
            if (root is AllCollectionResult) return null; // cannot compile All result
            if (root is CollectionOperator oper)
            {
                root = oper.Root;
            }
            else if(root is CollectionResult res)
            {
                root = res.Root;
            }
            else if (root is CollectionRootExp op)
            {
                root = op.Collection;
            }
            else if (root is DataSourceExp ds)
            {
                dataSource = ds;
                break;
            }
            else
            {
                root = null;
            }
        }
        if (dataSource == null) return null;

        #endregion
        
        AppSchemaDataResult resultType = AppSchemaDataResult.List;
        string? dataField = null;
        Expression? take = null;
        Expression? skip = null;
        Expression? filter = null;
        List<AppSchemaDataOrder> orders = [];

        // handle source first
        if (exp is CollectionResult dataResultExp)
        {
            resultType = dataResultExp switch
            {
                CountCollectionResult => AppSchemaDataResult.Count,
                AnyCollectionResult => AppSchemaDataResult.Exist,
                FirstCollectionResult => AppSchemaDataResult.First,
                LastCollectionResult => AppSchemaDataResult.Last,
                FieldsCollectionResult => AppSchemaDataResult.Field,
                _ => resultType
            };
            dataField = (dataResultExp as FieldsCollectionResult)?.Field;
            exp = dataResultExp.Root;
            
            // predicate check
            if (dataResultExp is PredicateCollectionResult { Predicate: not null } predicateResultExp)
                filter = await CompileDataSourceFilter(context, predicateResultExp.Predicate);
        }
        
        string app = dataSource.Source.App;
        string field = dataSource.Source.Field;

        SchemaExp? curr = exp;
        while (curr != null)
        {
            switch (curr)
            {
                case PredicateCollectionOperator whereExp:
                    filter = filter != null 
                        ? Expression.New(typeof(AppSchemaDataFilterBinary).GetConstructors()[0], Expression.Constant(LogicType.AndAlso), 
                            filter, await CompileDataSourceFilter(context, whereExp.Predicate))
                        : await CompileDataSourceFilter(context, whereExp.Predicate);
                    curr = whereExp.Root;
                    break;
                case OrderByCollectionOperator orderByExp:
                    orders.Add(new AppSchemaDataOrder(orderByExp.OrderField, orderByExp.Descending));
                    curr = orderByExp.Root;
                    break;
                case TakeCollectionOperator takeExp:
                    take = await context.CompileSchemaExpAsync(takeExp.Take);
                    curr = takeExp.Root;
                    break;
                case SkipCollectionOperator skipExp:
                    skip = await context.CompileSchemaExpAsync(skipExp.Skip);
                    curr = skipExp.Root;
                    break;
                default:
                    curr = null;
                    break;
            }
        }

        // Build data source expression
        MethodInfo queryMethod = typeof(AppDataQueryExtension).GetMethod(nameof(AppDataQueryExtension.GetSchemaDataAsync))!;
        MethodCallExpression callExp = Expression.Call(null,
            queryMethod,
            context.GetContext(),
            Expression.Constant(app),
            Expression.Constant(field),
            Expression.Constant(null, typeof(string)),
            Expression.Constant(resultType),
            filter ?? Expression.Constant(null, typeof(AppSchemaDataFilter)),
            skip != null ? context.ConvertExp(typeof(int), skip) : Expression.Constant(0, typeof(int)),
            take != null ? context.ConvertExp(typeof(int), take) : Expression.Constant(0, typeof(int)),
            Expression.Constant(false, typeof(bool)),
            Expression.Constant(orders.Count > 0 ? orders.ToArray() : null, typeof(AppSchemaDataOrder[])),
            dataField != null ? Expression.Constant(dataField) : Expression.Constant(null, typeof(string))
        );
        callExp = Expression.Call(callExp, callExp.Type.GetMethod(nameof(Task.GetAwaiter), Type.EmptyTypes)!);
        return Expression.Call(callExp, callExp.Type.GetMethod(nameof(System.Runtime.CompilerServices.TaskAwaiter<dynamic>.GetResult), Type.EmptyTypes)!);
    }

    async Task<Expression> CompileDataSourceFilter(CompileContext context, SchemaExp exp,
        Dictionary<SchemaExp, SchemaExp>? expReplace = null)
    {
        // Try inline function call
        if (exp is FuncCallExp funcCall)
        {
            FunctionTypeSchema inlineFunc = await context.VisitFunctionTypeAsync(funcCall.Function);
            if (inlineFunc.Exps is [{ Value: LogicExp inlineExp }])
            {
                // Replace parameters
                Dictionary<SchemaExp, SchemaExp> paramMap = new();
                for (int i = 0; i < inlineFunc.Args.Length; i++)
                {
                    ArgumentExp arg = inlineFunc.Args[i];
                    SchemaExp? item = funcCall.Args.ElementAtOrDefault(i);
                    if (item == null)
                    {
                        paramMap[arg] = arg.Nullable
                            ? new ConstantExp(arg.ValueType.From(null))
                            : throw new FunctionVisitException(ErrorCodes.FUNC_EXP_WRONG_ARGS);
                    }
                    else
                    {
                        paramMap[arg] = item;
                    }
                }

                try
                {

                    return await CompileDataSourceFilter(context, inlineExp, paramMap);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
        }

        exp = ReplaceExp(exp);
        
        return exp switch
        {
            FieldAccessExp fieldExp => ReplaceExp(fieldExp.Owner) is CollectionItemExp 
                ? Expression.New(typeof(AppSchemaDataFilterField).GetConstructors()[0], Expression.Constant(fieldExp.FieldName))
                : Expression.New(typeof(AppSchemaDataFilterValue).GetConstructors()[0], 
                    await context.CompileSchemaExpAsync(new FieldAccessExp(ReplaceExp(fieldExp.Owner), fieldExp.FieldName, fieldExp.ValueType))),
            VariableExp varExp => await CompileDataSourceFilter(context, varExp.Value, expReplace),
            DefaultExp dftExp => await CompileDataSourceFilter(context, dftExp.Inner, expReplace), // unpack the default
            UnaryLogicExp unaryExp => Expression.New(typeof(AppSchemaDataFilterUnary).GetConstructors()[0], Expression.Constant(unaryExp.Type), await CompileDataSourceFilter(context, unaryExp.Inner, expReplace)),
            BinaryLogicExp binaryExp => Expression.New(typeof(AppSchemaDataFilterBinary).GetConstructors()[0], Expression.Constant(binaryExp.Type), await CompileDataSourceFilter(context, binaryExp.Left, expReplace), await CompileDataSourceFilter(context, binaryExp.Right, expReplace)),
            _ => Expression.New(typeof(AppSchemaDataFilterValue).GetConstructors()[0], await context.CompileSchemaExpAsync(exp, typeof(object))),
        };

        SchemaExp ReplaceExp(SchemaExp e)
        {
            if (expReplace == null) return e;
            if (expReplace.TryGetValue(e, out SchemaExp? r))
                return r;
            else if(e is VariableExp varExp && expReplace.TryGetValue(varExp.Value, out SchemaExp? rv))
                return rv;
            return e;
        }
    }
}