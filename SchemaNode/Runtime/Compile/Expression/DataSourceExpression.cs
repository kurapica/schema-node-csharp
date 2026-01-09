using SchemaNode.Components;
using SchemaNode.Enum;
using SchemaNode.Function;
using SchemaNode.Node;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json.Nodes;
using static SchemaNode.Utility.Constant;
using ExpressionType = SchemaNode.Enum.ExpressionType;

// ReSharper disable NotAccessedPositionalProperty.Global

namespace SchemaNode.Runtime;

#region Data Source Exp

/// <summary>
/// The data source
/// </summary>
public record DataSource(string App, string Field, SchemaExpression? Target, AnySchemaType SchemaType);

/// <summary>
/// The data source expression
/// </summary>
public record DataSourceExpression(DataSource Source) : SchemaExpression(Source.SchemaType);

/// <summary>
/// A ref expression for data source struct type
/// </summary>
public record DataSourceRefExpression(string App, string Field, AnySchemaType SchemaType) : SchemaExpression(SchemaType);

/// <summary>
/// The filter data source expression
/// </summary>
public record WhereDataSourceExpression(DataSourceExpression Previous, LogicExpression Filter) : DataSourceExpression(Previous.Source);

/// <summary>
/// The order by data source expression
/// </summary>
public record OrderByDataSourceExpression(DataSourceExpression Previous, string OrderField, bool Descending) : DataSourceExpression(Previous.Source);

/// <summary>
/// The take data source expression
/// </summary>
public record TakeDataSourceExpression(DataSourceExpression Previous, SchemaExpression TakeExp) : DataSourceExpression(Previous.Source);

/// <summary>
/// The skip data source expression
/// </summary>
public record SkipDataSourceExpression(DataSourceExpression Previous, SchemaExpression SkipExp) : DataSourceExpression(Previous.Source);

#endregion

#region Data Result Exp

/// <summary>
/// The data source result
/// </summary>
public abstract record DataResultExpression(DataSourceExpression Source, AnySchemaType SchemaType): SchemaExpression(SchemaType);

/// <summary>
/// The count data source expression
/// </summary>
public record CountDataSourceExpression(DataSourceExpression Source, AnySchemaType SchemaType) : DataResultExpression(Source, SchemaType);

/// <summary>
/// Exists data source expression
/// </summary>
public record ExistsDataSourceExpression(DataSourceExpression Source, AnySchemaType SchemaType) : DataResultExpression(Source, SchemaType);

/// <summary>
/// No exists data source expression
/// </summary>
public record NoExistsDataSourceExpression(DataSourceExpression Source, AnySchemaType SchemaType) : DataResultExpression(Source, SchemaType);

/// <summary>
/// The first data source expression
/// </summary>
public record FirstDataSourceExpression(DataSourceExpression Source) : DataResultExpression(Source, (Source.SchemaType as ArrayType)!.ElementSchemaType!);

/// <summary>
/// The last data source expression
/// </summary>
public record LastDataSourceExpression(DataSourceExpression Source) : DataResultExpression(Source, (Source.SchemaType as ArrayType)!.ElementSchemaType!);

/// <summary>
/// The field access data source expression
/// </summary>
public record FieldsDataSourceExpression(DataSourceExpression Source, string FieldName, AnySchemaType SchemaType) : DataResultExpression(Source, SchemaType);

#endregion

#region Sql query model buld from exp

public abstract record AppSchemaDataFilter;

public enum AppSchemaDataResult
{
    List,
    Count,
    Exist,
    NotExist,
    First,
    Last,
    Field,
}

public record AppSchemaDataFilterField(string Field): AppSchemaDataFilter;

public record AppSchemaDataFilterUnary(LogicExpType Type, AppSchemaDataFilter Operand) : AppSchemaDataFilter;

public record AppSchemaDataFilterBinary(LogicExpType Type, AppSchemaDataFilter Left, AppSchemaDataFilter Right) : AppSchemaDataFilter;

public record AppSchemaDataFilterValue(object Value) : AppSchemaDataFilter;

public record AppSchemaDataOrder(string Field, bool Desc);

public static class AppSchemaDataFilterExtensions
{
    /// <summary>
    /// Combine two filters with AND ALSO
    /// </summary>
    public static AppSchemaDataFilter AndAlso(this AppSchemaDataFilter left, AppSchemaDataFilter right)
        => left is AppSchemaDataFilterValue ? right
                : right is AppSchemaDataFilterValue ? left
                : new AppSchemaDataFilterBinary(LogicExpType.AndAlso, left, right);

    /// <summary>
    /// Combine the access exp with the filter
    /// </summary>
    public static AppSchemaDataFilter Combine(this AppSchemaDataFilter accessExp, StructType structType, JsonObject? filter = null)
    {
        if (filter == null || filter.IsEmpty()) return accessExp;

        foreach ((string key, JsonNode? value) in filter)
        {
            if (value == null || value.IsEmpty()) continue;
            StructFieldConfig? field = structType.GetField(key);
            if (field is not { SchemeType: ScalarType }) continue;

            AppSchemaDataFilter? fieldExp = accessExp.FindFieldAccessOper(key);

            // additional filter
            if (fieldExp is not AppSchemaDataFilterBinary { Type: LogicExpType.Equal } binaryEqual)
            {
                accessExp = value switch
                {
                    JsonArray arr => new AppSchemaDataFilterBinary(LogicExpType.AndAlso, accessExp,
                        new AppSchemaDataFilterBinary(LogicExpType.Contains,
                            new AppSchemaDataFilterValue(new ArrayTypeNode(field.SchemeType!, arr)),
                            new AppSchemaDataFilterField(key))),
                    JsonValue val => new AppSchemaDataFilterBinary(LogicExpType.AndAlso, accessExp,
                        new AppSchemaDataFilterBinary(LogicExpType.Equal, new AppSchemaDataFilterField(key),
                            new AppSchemaDataFilterValue(field.SchemeType!.CreateNode(val)!))),
                    _ => accessExp
                };
            }

            // write back to the filter
            else
            {
                filter[key] = binaryEqual.Right is AppSchemaDataFilterValue valNode
                    ? valNode.Value is AnySchemaNode node ? node.ToJsonNode() : valNode.Value.ToJsonNode()
                    : null;
            }
        }

        return accessExp;
    }


    /// <summary>
    /// Try to convert the access exp to filter json object
    /// </summary>
    public static JsonObject? ToFilter(this AppSchemaDataFilter accessExp)
    {
        if (accessExp is AppSchemaDataFilterBinary binaryAccessExp)
        {
            if (binaryAccessExp.Type == LogicExpType.AndAlso)
            {
                JsonObject? leftFilter = binaryAccessExp.Left.ToFilter();
                JsonObject? rightFilter = binaryAccessExp.Right.ToFilter();
                if (leftFilter == null) return null;
                if (rightFilter == null) return null;

                // merge
                foreach ((string key, JsonNode? value) in rightFilter)
                    leftFilter[key] = value?.DeepClone();
                return leftFilter;
            }

            AppSchemaDataFilterField? accessNode = binaryAccessExp.Left as AppSchemaDataFilterField ??
                                                   binaryAccessExp.Right as AppSchemaDataFilterField;
            if (accessNode == null) return null;
            AppSchemaDataFilterValue? valueAccess = (binaryAccessExp.Left == accessNode
                ? binaryAccessExp.Right
                : binaryAccessExp.Left) as AppSchemaDataFilterValue;
            if (valueAccess == null) return null;

            if (binaryAccessExp.Type is LogicExpType.Equal or LogicExpType.Contains)
            {
                return new JsonObject
                {
                    [accessNode.Field] = valueAccess.Value.ToJson()
                };
            }
        }

        return null;
    }

    /// <summary>
    /// Convert the exp tree to SQL
    /// </summary>
    public static string ToSql(this AppSchemaDataFilter accessExp, ISqlProvider sqlProvider, string prefix = "")
        => ToSql(sqlProvider, accessExp, prefix);

    static AppSchemaDataFilter? FindFieldAccessOper(this AppSchemaDataFilter accessExp, string fieldName)
    {
        switch (accessExp)
        {
            case AppSchemaDataFilterField access:
                return access.Field == fieldName ? accessExp : null;
            case AppSchemaDataFilterUnary unary:
                return unary.Operand.FindFieldAccessOper(fieldName);
            case AppSchemaDataFilterBinary binary:
                return (binary.Left.FindFieldAccessOper(fieldName) ?? binary.Right.FindFieldAccessOper(fieldName)) != null ? accessExp : null;
            default:
                return null;
        }
    }

    // To sql
    static string ToSql(ISqlProvider sqlProvider, AppSchemaDataFilter accessExp, string prefix)
    {
        switch (accessExp)
        {
            case AppSchemaDataFilterField access:
                return $"{prefix}{sqlProvider.QuoteField(access.Field)}";
            case AppSchemaDataFilterUnary unary:
                switch (unary.Type)
                {
                    case LogicExpType.IsNull:
                    case LogicExpType.IsEmpty:
                        return sqlProvider.IsNull(ToSql(sqlProvider, unary.Operand, prefix));
                    case LogicExpType.NotNull:
                    case LogicExpType.NotEmpty:
                        return sqlProvider.IsNotNull(ToSql(sqlProvider, unary.Operand, prefix));
                    default:
                        throw new NotSupportedException($"The unary expression type not supported: {unary.Type}");
                }
            case AppSchemaDataFilterBinary binary:
                switch (binary.Type)
                {
                    case LogicExpType.AndAlso:
                    case LogicExpType.OrElse:
                    case LogicExpType.Equal:
                    case LogicExpType.NotEqual:
                    case LogicExpType.GreaterThan:
                    case LogicExpType.GreaterEqual:
                    case LogicExpType.LessThan:
                    case LogicExpType.LessEqual:
                        return sqlProvider.Binary(binary.Type,
                            ToSql(sqlProvider, binary.Left, prefix),
                            ToSql(sqlProvider, binary.Right, prefix));
                    case LogicExpType.Contains:
                        return sqlProvider.In(
                            ToSql(sqlProvider, binary.Right, prefix),
                            ((binary.Left as AppSchemaDataFilterValue)!.Value as IEnumerable<object>)!);
                    case LogicExpType.NotContains:
                        return sqlProvider.NotIn(
                            ToSql(sqlProvider, binary.Right, prefix),
                            ((binary.Left as AppSchemaDataFilterValue)!.Value as IEnumerable<object>)!);
                    case LogicExpType.StartsWith:
                        return sqlProvider.LikeStartsWith(
                            ToSql(sqlProvider, binary.Left, prefix),
                            (string)typeof(string).TryConvert((binary.Right as AppSchemaDataFilterValue)?.Value
                                ?? throw new NotSupportedException("The startsWith right value must be string"))!);
                    case LogicExpType.NotStartsWith:
                        return sqlProvider.NotLikeStartsWith(
                            ToSql(sqlProvider, binary.Left, prefix),
                            (string)typeof(string).TryConvert((binary.Right as AppSchemaDataFilterValue)?.Value
                                ?? throw new NotSupportedException("The notStartsWith right value must be string"))!);
                    case LogicExpType.EndsWith:
                        return sqlProvider.LikeEndsWith(
                            ToSql(sqlProvider, binary.Left, prefix),
                            (string)typeof(string).TryConvert((binary.Right as AppSchemaDataFilterValue)?.Value
                                ?? throw new NotSupportedException("The endsWith right value must be string"))!);
                    case LogicExpType.NotEndsWith:
                        return sqlProvider.NotLikeEndsWith(
                            ToSql(sqlProvider, binary.Left, prefix),
                            (string)typeof(string).TryConvert((binary.Right as AppSchemaDataFilterValue)?.Value
                                ?? throw new NotSupportedException("The notEndsWith right value must be string"))!);
                    case LogicExpType.Match:
                        return sqlProvider.LikeContains(
                            ToSql(sqlProvider, binary.Left, prefix),
                            (string)typeof(string).TryConvert((binary.Right as AppSchemaDataFilterValue)?.Value
                                ?? throw new NotSupportedException("The match right value must be string"))!);
                    case LogicExpType.NotMatch:
                        return sqlProvider.NotLikeContains(
                            ToSql(sqlProvider, binary.Left, prefix),
                            (string)typeof(string).TryConvert((binary.Right as AppSchemaDataFilterValue)?.Value
                                ?? throw new NotSupportedException("The notMatch right value must be string"))!);
                    default:
                        throw new NotSupportedException($"The binary expression type not supported: {binary.Type}");
                }
            case AppSchemaDataFilterValue value:
                return sqlProvider.Literal(value.Value);
        }

        throw new NotSupportedException("The expression type not supported");
    }

}

#endregion

/// <summary>
/// The data source visitor
/// </summary>
public class DataSourceExpressionVisitor : IExpressionVisitor
{
    /// <inheritdoc />
    public int Priority => EXP_DATA_SOURCE_PRIORITY;

    /// <inheritdoc />
    public async Task<SchemaExpression?> VisitExpAsync(CompileContext context, SchemaExpression exp)
    {
        if (exp is not FuncCallExpression callExp) return null;

        #region Data Source

        // Data source check
        if (callExp.Function.Name == $"{NS_SYSTEM_DATA}.{nameof(SystemData.getdatasource)}")
        {
            string? app = callExp.Args.ElementAtOrDefault(0) is ConstantExpression appExp ? appExp.Value.ToValue<string>() :  null;
            string? field = callExp.Args.ElementAtOrDefault(1) is ConstantExpression fldExp ? fldExp.Value.ToValue<string>() :  null;

            // App & Field must be provided
            if (string.IsNullOrEmpty(app) || string.IsNullOrEmpty(field))
                throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);
            
            // App & Field must be valid
            AppType? appType = await context.GetAppTypeAsync(app);
            AppFieldType? appField = appType?.GetField(field);
            AnySchemaType? schemaType = appField?.SchemaType;
            if (schemaType == null && !string.IsNullOrEmpty(appField?.Type))
                schemaType = await context.GetSchemaTypeAsync(appField.Type);
            return schemaType is ArrayType { ElementSchemaType: StructType, Primary: { Length: > 0 } }
                ? new DataSourceExpression(new DataSource(app, field, callExp.Args.ElementAtOrDefault(2), schemaType))
                : null; // call directly
        }

        #endregion

        #region Linq

        // Indicate the source expression
        DataSourceExpression? sourceExp = callExp.Args.FirstOrDefault(a => a is DataSourceExpression) as DataSourceExpression;
        IteratorExpression? iter = callExp.Args.FirstOrDefault(a => a is IteratorExpression { Array: DataSourceExpression or FieldAccessExpression { Owner: DataSourceExpression } }) as IteratorExpression;
        if (sourceExp == null && iter == null) return null;

        // Non-filter call
        switch (callExp.ExpType)
        {
            // Direct Call
            case ExpressionType.Call:
            {
                if (sourceExp == null) return null;
            
                switch (callExp.Function.Name)
                {
                    // getFields(source)
                    case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.getfields)}":
                    {
                        // not support a.b.c deep field access
                        string fieldName = callExp.Args.ElementAtOrDefault(1) is ConstantExpression fieldExp ? fieldExp.Value.ToValue<string>() ?? "" : "";
                        if (string.IsNullOrEmpty(fieldName) || sourceExp.SchemaType is not ArrayType { ElementSchemaType: StructType structType } || structType.GetField(fieldName) == null)
                            throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);
                        return new FieldsDataSourceExpression(sourceExp, fieldName, (await context.GetArrayType(structType.GetField(fieldName)!.SchemeType!))!);
                    }
                
                    // source.length
                    case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.arrlen)}":
                        return new CountDataSourceExpression(sourceExp, (await context.GetSchemaTypeAsync(NS_SYSTEM_INT))!);
                
                    // source.OrderBy(field, desc)
                    case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.orderby)}":
                    {
                        string orderField = callExp.Args.ElementAtOrDefault(1) is ConstantExpression fieldExp ? fieldExp.Value.ToValue<string>() ?? "" : "";
                        bool descending = callExp.Args.ElementAtOrDefault(2) is ConstantExpression descExp && descExp.Value.ToValue<bool>();

                        if (string.IsNullOrEmpty(orderField) || sourceExp.SchemaType is not ArrayType { ElementSchemaType: StructType structType } || structType.GetField(orderField) == null)
                            throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);
                    
                        return new  OrderByDataSourceExpression(sourceExp, orderField, descending);
                    }
                
                    // source.skip(n)
                    case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.skip)}":
                        return new SkipDataSourceExpression(sourceExp, callExp.Args.ElementAtOrDefault(1)!);
                
                    // source.take(n)
                    case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.take)}":
                        return new TakeDataSourceExpression(sourceExp, callExp.Args.ElementAtOrDefault(1)!);
                }

                break;
            }

            // Map, only handle field access
            case ExpressionType.Map:
            {
                if (iter == null) return null;

                switch (callExp.Function.Name)
                {
                    // getField(source, field), cover the case to FieldsDataSourceExpression
                    case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.getfield)}":
                    case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.getfielddefault)}":
                    {
                        if (iter.Array is not DataSourceExpression source) return null;

                        string fieldName = callExp.Args.ElementAtOrDefault(1) is ConstantExpression fieldExp ? fieldExp.Value.ToValue<string>() ?? "" : "";
                        if (string.IsNullOrEmpty(fieldName) || source.SchemaType is not ArrayType { ElementSchemaType: StructType structType } || structType.GetField(fieldName) == null)
                            throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);
                        return new FieldsDataSourceExpression(source, fieldName, (await context.GetArrayType(structType.GetField(fieldName)!.SchemeType!))!);
                    }

                    // assign
                    case $"{NS_SYSTEM_CONV}.{nameof(SystemConv.assign)}":
                    case $"{NS_SYSTEM_CONV}.{nameof(SystemConv.@default)}":
                    {
                        if (iter.Array is not FieldAccessExpression { Owner: DataSourceExpression source } fa || fa.FieldName.Contains('.')) return null;
                        return new FieldsDataSourceExpression(source, fa.FieldName, (await context.GetArrayType(((source.SchemaType as ArrayType)!.ElementSchemaType as StructType)!.GetField(fa.FieldName)!.SchemeType!))!);
                    }
                }
                return null;
            }

            // Ignore reduce
            case ExpressionType.Reduce:
                return null;
        }
        
        // All other calls must have iterator
        if (iter == null) return null;
        sourceExp = iter.Array as DataSourceExpression ?? (iter.Array as FieldAccessExpression)?.Owner as DataSourceExpression;
        
        // Filter - only support system define functions
        DataSourceRefExpression refExp = new DataSourceRefExpression(sourceExp!.Source.App, sourceExp.Source.Field, (sourceExp.SchemaType as ArrayType)!.ElementSchemaType!);
        SchemaExpression[] refArgs = callExp.Args.Select(a => a == iter
            ? (iter.Array is FieldAccessExpression fldAccess
                ? new FieldAccessExpression(refExp, fldAccess.FieldName, fldAccess.SchemaType)
                : refExp)
            : a).ToArray();
        
        // Must be boolean return type
        SchemaExpression filterExp = new FuncCallExpression(callExp.Function, refArgs, (await context.GetSchemaTypeAsync(NS_SYSTEM_BOOL))!);
        filterExp = await context.VisitSchemaExpAsync(filterExp);
        
        // Must be logic expression
        if (filterExp is not LogicExpression logicExp) 
            throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);

        // Generate filter result
        WhereDataSourceExpression filterResult = sourceExp is WhereDataSourceExpression whereSource
            ? new WhereDataSourceExpression(whereSource.Previous, new BinaryLogicExpression(LogicExpType.AndAlso, whereSource.Filter, logicExp, logicExp.SchemaType))
            : new WhereDataSourceExpression(sourceExp, logicExp);

        // Handle other expression types
        return callExp.ExpType switch
        {
            ExpressionType.Filter => filterResult,
            ExpressionType.First => new FirstDataSourceExpression(filterResult),
            ExpressionType.Last => new LastDataSourceExpression(filterResult),
            ExpressionType.Count => new CountDataSourceExpression(filterResult, (await context.GetSchemaTypeAsync(NS_SYSTEM_INT))!),
            ExpressionType.Any => new ExistsDataSourceExpression(filterResult, (await context.GetSchemaTypeAsync(NS_SYSTEM_BOOL))!),
            ExpressionType.All => new NoExistsDataSourceExpression(filterResult, (await context.GetSchemaTypeAsync(NS_SYSTEM_BOOL))!),
            _ => null
        };

        #endregion
    }

    /// <inheritdoc />
    public async Task<Expression?> CompileExpAsync(CompileContext context, SchemaExpression exp)
    {
        if (exp is not DataSourceExpression && exp is not DataResultExpression) return null;

        AppSchemaDataResult resultType = AppSchemaDataResult.List;
        string? dataField = null;
        Expression? take = null;
        Expression? skip = null;
        Expression? filter = null;
        List<AppSchemaDataOrder> orders = [];

        // handle source first
        if (exp is DataResultExpression dataResultExp)
        {
            resultType = dataResultExp switch
            {
                CountDataSourceExpression => AppSchemaDataResult.Count,
                ExistsDataSourceExpression => AppSchemaDataResult.Exist,
                NoExistsDataSourceExpression => AppSchemaDataResult.NotExist,
                FirstDataSourceExpression => AppSchemaDataResult.First,
                LastDataSourceExpression => AppSchemaDataResult.Last,
                FieldsDataSourceExpression => AppSchemaDataResult.Field,
                _ => resultType
            };
            dataField = (dataResultExp as FieldsDataSourceExpression)?.FieldName;
            exp = dataResultExp.Source;
        }

        DataSourceExpression? sourceExp = exp as DataSourceExpression;
        string app = sourceExp!.Source.App;
        string field = sourceExp.Source.Field;
        Expression? target = null;

        while (sourceExp != null)
        {
            target ??= sourceExp.Source.Target != null
                ? await context.CompileSchemaExpAsync(sourceExp.Source.Target)
                : null;

            switch (sourceExp)
            {
                case WhereDataSourceExpression whereExp:
                    filter = filter != null 
                        ? Expression.New(typeof(AppSchemaDataFilterBinary).GetConstructors()[0], Expression.Constant(LogicExpType.AndAlso), filter, await CompileDataSourceFilter(context, whereExp.Filter))
                        : await CompileDataSourceFilter(context, whereExp.Filter);
                    sourceExp = whereExp.Previous;
                    break;
                case OrderByDataSourceExpression orderByExp:
                    orders.Add(new AppSchemaDataOrder(orderByExp.OrderField, orderByExp.Descending));
                    sourceExp = orderByExp.Previous;
                    break;
                case TakeDataSourceExpression takeExp:
                    take = await context.CompileSchemaExpAsync(takeExp.TakeExp);
                    sourceExp = takeExp.Previous;
                    break;
                case SkipDataSourceExpression skipExp:
                    skip = await context.CompileSchemaExpAsync(skipExp.SkipExp);
                    sourceExp = skipExp.Previous;
                    break;
                default:
                    sourceExp = null;
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

    async Task<Expression> CompileDataSourceFilter(CompileContext context, SchemaExpression exp)
    {
        return exp switch
        {
            FieldAccessExpression fieldExp => Expression.New(typeof(AppSchemaDataFilterField).GetConstructors()[0], Expression.Constant(fieldExp.FieldName)),
            UnaryLogicExpression unaryExp => Expression.New(typeof(AppSchemaDataFilterUnary).GetConstructors()[0], Expression.Constant(unaryExp.Type), await CompileDataSourceFilter(context, unaryExp.Inner)),
            BinaryLogicExpression binaryExp => Expression.New(typeof(AppSchemaDataFilterBinary).GetConstructors()[0], Expression.Constant(binaryExp.Type), await CompileDataSourceFilter(context, binaryExp.Left), await CompileDataSourceFilter(context, binaryExp.Right)),
            _ => Expression.New(typeof(AppSchemaDataFilterValue).GetConstructors()[0], await context.CompileSchemaExpAsync(exp)),
        };
    }
}