using SchemaNode.Components;
using SchemaNode.Enum;
using SchemaNode.Function;
using System.Linq.Expressions;
using System.Reflection;
using static SchemaNode.Utility.Constant;
using ExpressionType = SchemaNode.Enum.ExpressionType;

// ReSharper disable NotAccessedPositionalProperty.Global

namespace SchemaNode.Runtime;

#region Data Source Exp

/// <summary>
/// The data source
/// </summary>
public record DataSource(string App, string Field, SchemaExp? Target, AnySchemaType SchemaType);

/// <summary>
/// The data source expression
/// </summary>
public record DataSourceExp(DataSource Source) : SchemaExp(Source.SchemaType);

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
        if (exp is not FuncCallExp { ExpType: ExpressionType.Call } callExp) return null;

        // Data source check
        if (callExp.Function.Name != $"{NS_SYSTEM_DATA}.{nameof(SystemData.getdatasource)}") return null;
        string? app = callExp.Args.ElementAtOrDefault(0) is ConstantExp appExp ? appExp.Value.ToValue<string>() :  null;
        string? field = callExp.Args.ElementAtOrDefault(1) is ConstantExp fldExp ? fldExp.Value.ToValue<string>() :  null;

        // App & Field must be provided
        if (string.IsNullOrEmpty(app) || string.IsNullOrEmpty(field))
            throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs);
            
        // App & Field must be valid
        AppType? appType = await context.GetAppTypeAsync(app);
        AppFieldType? appField = appType?.GetField(field);
        AnySchemaType? schemaType = appField?.SchemaType;
        if (schemaType == null && !string.IsNullOrEmpty(appField?.Type))
            schemaType = await context.GetSchemaTypeAsync(appField.Type);
        return schemaType is ArrayType { ElementSchemaType: StructType, Primary: { Length: > 0 } }
            ? new DataSourceExp(new DataSource(app, field, callExp.Args.ElementAtOrDefault(2), schemaType))
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
        }
        
        string app = dataSource.Source.App;
        string field = dataSource.Source.Field;
        Expression? target = dataSource.Source.Target != null
            ? await context.CompileSchemaExpAsync(dataSource.Source.Target)
            : null;

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
            target ?? Expression.Constant(null, typeof(string)),
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

    async Task<Expression> CompileDataSourceFilter(CompileContext context, SchemaExp exp, Dictionary<SchemaExp, SchemaExp>? expReplace = null)
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
                        paramMap[arg] = arg.Nullable ? new ConstantExp(arg.SchemaType.CreateNode(null)!)
                                : throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs);
                    }
                    else
                    {
                        paramMap[arg] = item;
                    }
                }
                return await CompileDataSourceFilter(context, inlineExp, paramMap); 
            }
        }
        
        if (expReplace != null && expReplace.TryGetValue(exp, out SchemaExp? replacedExp))
            exp = replacedExp;
        
        return exp switch
        {
            FieldAccessExp fieldExp => fieldExp.Owner is CollectionItemExp 
                                       || expReplace != null && expReplace.TryGetValue(fieldExp.Owner, out SchemaExp? rep) && rep is CollectionItemExp
                ? Expression.New(typeof(AppSchemaDataFilterField).GetConstructors()[0], Expression.Constant(fieldExp.FieldName))
                : Expression.New(typeof(AppSchemaDataFilterValue).GetConstructors()[0], await context.CompileSchemaExpAsync(new FieldAccessExp(
                    expReplace != null && expReplace.TryGetValue(fieldExp.Owner, out SchemaExp? newOwner) 
                        ? newOwner 
                        : fieldExp.Owner, fieldExp.FieldName, fieldExp.SchemaType
                    ))),
            UnaryLogicExp unaryExp => Expression.New(typeof(AppSchemaDataFilterUnary).GetConstructors()[0], Expression.Constant(unaryExp.Type), await CompileDataSourceFilter(context, unaryExp.Inner)),
            BinaryLogicExp binaryExp => Expression.New(typeof(AppSchemaDataFilterBinary).GetConstructors()[0], Expression.Constant(binaryExp.Type), await CompileDataSourceFilter(context, binaryExp.Left), await CompileDataSourceFilter(context, binaryExp.Right)),
            _ => Expression.New(typeof(AppSchemaDataFilterValue).GetConstructors()[0], await context.CompileSchemaExpAsync(exp)),
        };
    }
}