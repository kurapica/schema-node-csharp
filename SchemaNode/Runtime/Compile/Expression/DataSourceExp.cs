using SchemaNode.Components;
using SchemaNode.Enum;
using SchemaNode.Function;
using SchemaNode.Node;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json.Nodes;
using SchemaNode.Context;
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

/// <summary>
/// A ref expression for data source struct type
/// </summary>
public record DataSourceRefExp(string App, string Field, AnySchemaType SchemaType) : SchemaExp(SchemaType);

/// <summary>
/// The filter data source expression
/// </summary>
public record WhereDataSourceExp(DataSourceExp Previous, LogicExp Filter) : DataSourceExp(Previous.Source);

/// <summary>
/// The order by data source expression
/// </summary>
public record OrderByDataSourceExp(DataSourceExp Previous, string OrderField, bool Descending) : DataSourceExp(Previous.Source);

/// <summary>
/// The take data source expression
/// </summary>
public record TakeDataSourceExp(DataSourceExp Previous, SchemaExp TakeExp) : DataSourceExp(Previous.Source);

/// <summary>
/// The skip data source expression
/// </summary>
public record SkipDataSourceExp(DataSourceExp Previous, SchemaExp SkipExp) : DataSourceExp(Previous.Source);

#endregion

#region Data Result Exp

/// <summary>
/// The data source result
/// </summary>
public abstract record DataResultExp(DataSourceExp Source, AnySchemaType SchemaType): SchemaExp(SchemaType);

/// <summary>
/// The count data source expression
/// </summary>
public record CountDataSourceExp(DataSourceExp Source, AnySchemaType SchemaType) : DataResultExp(Source, SchemaType);

/// <summary>
/// Exists data source expression
/// </summary>
public record ExistsDataSourceExp(DataSourceExp Source, AnySchemaType SchemaType) : DataResultExp(Source, SchemaType);

/// <summary>
/// No exists data source expression
/// </summary>
public record NoExistsDataSourceExp(DataSourceExp Source, AnySchemaType SchemaType) : DataResultExp(Source, SchemaType);

/// <summary>
/// The first data source expression
/// </summary>
public record FirstDataSourceExp(DataSourceExp Source) : DataResultExp(Source, (Source.SchemaType as ArrayType)!.ElementSchemaType!);

/// <summary>
/// The last data source expression
/// </summary>
public record LastDataSourceExp(DataSourceExp Source) : DataResultExp(Source, (Source.SchemaType as ArrayType)!.ElementSchemaType!);

/// <summary>
/// The field access data source expression
/// </summary>
public record FieldsDataSourceExp(DataSourceExp Source, string FieldName, AnySchemaType SchemaType) : DataResultExp(Source, SchemaType);

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

public record AppSchemaDataFilterUnary(LogicType Type, AppSchemaDataFilter Operand) : AppSchemaDataFilter;

public record AppSchemaDataFilterBinary(LogicType Type, AppSchemaDataFilter Left, AppSchemaDataFilter Right) : AppSchemaDataFilter;

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
                : new AppSchemaDataFilterBinary(LogicType.AndAlso, left, right);

    internal static async Task<AppSchemaDataFilter?> ToAppSchemaDataFilterAsync(this JsonObject filter, SchemaContext context, StructType structType, FieldFilter[]? fieldFilters = null)
    {
        if (filter.IsEmpty()) return null;

        AppSchemaDataFilter? accessExp = null;
        foreach ((string key, JsonNode? value) in filter)
        {
            if (value == null || value.IsEmpty()) continue;
            
            // filter
            var filterMode = fieldFilters?.FirstOrDefault(f => f.Filter.Equals(key, StringComparison.OrdinalIgnoreCase))?.Mode 
                             ?? FieldFilterMode.Exactly;

            if (filterMode == FieldFilterMode.Filter)
            {
                // get the function
                FunctionType? funcType = await context.GetSchemaTypeAsync<FunctionType>(key);
                if (funcType != null && value is JsonArray { Count: > 0 } arr)
                {
                    // Call filter func with policy filter compile context
                    AppSchemaDataFilter? f = await funcType.CallAsync<AppSchemaDataFilter, QueryFilterCompileContext>(context, arr.Select(a => (object)a!).ToArray());
                    if (f != null)
                    {
                        accessExp = accessExp != null
                            ? new AppSchemaDataFilterBinary(LogicType.AndAlso, accessExp, f)
                            : f;
                    }
                }
                continue;
            }
            
            // get the field
            StructFieldConfig? field = structType.GetField(key);
            
            // only support scalar or locale string type
            if (field is not { SchemeType: ScalarType } && !NS_SYSTEM_LOCALE_STRING.Equals(field?.SchemeType?.Name)) continue;
            
            var filterExp = value switch
            {
                JsonArray arr => new AppSchemaDataFilterBinary(LogicType.Contains,
                        new AppSchemaDataFilterValue(new ArrayTypeNode(field.SchemeType, arr)),
                        new AppSchemaDataFilterField(key)),
                JsonValue val => new AppSchemaDataFilterBinary(filterMode switch
                        {
                            FieldFilterMode.Exactly => LogicType.Equal,
                            FieldFilterMode.Prefix => LogicType.StartsWith,
                            FieldFilterMode.Suffix => LogicType.EndsWith,
                            FieldFilterMode.Contains => LogicType.Match,
                            _ => LogicType.Equal
                        }, 
                        new AppSchemaDataFilterField(key),
                        new AppSchemaDataFilterValue(field.SchemeType.CreateNode(val)!)),
                _ => null
            };
            
            accessExp = accessExp != null && filterExp != null
                ? new AppSchemaDataFilterBinary(LogicType.AndAlso, accessExp, filterExp)
                : filterExp;
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
            if (binaryAccessExp.Type == LogicType.AndAlso)
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

            if (binaryAccessExp.Type is LogicType.Equal or LogicType.Contains)
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
    public static string ToSql(this AppSchemaDataFilter accessExp, ISqlProvider sqlProvider, DynamicTableSchema tableSchema, string prefix = "")
        => ToSql(sqlProvider, accessExp, tableSchema, prefix);

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
    static string ToSql(ISqlProvider sqlProvider, AppSchemaDataFilter accessExp, DynamicTableSchema tableSchema, string prefix)
    {
        switch (accessExp)
        {
            case AppSchemaDataFilterField access:
            {
                // Check if the field is complex field
                string fieldName = "";
                foreach (DynamicTableField field in  tableSchema.GetDynamicTableFields(access.Field))
                {
                    // Works for locale string key
                    if (field is { Complex: not null, SchemaType: not ScalarType }) continue;
                    fieldName = field.Name;
                    break;
                }
                if (string.IsNullOrWhiteSpace(fieldName))
                    throw new NotSupportedException($"The field not found in table schema: {access.Field}");
                return $"{prefix}{sqlProvider.QuoteField(fieldName)}";
            }
            case AppSchemaDataFilterUnary unary:
                switch (unary.Type)
                {
                    case LogicType.IsNull:
                    case LogicType.IsEmpty:
                        return sqlProvider.IsNull(ToSql(sqlProvider, unary.Operand, tableSchema, prefix));
                    case LogicType.NotNull:
                    case LogicType.NotEmpty:
                        return sqlProvider.IsNotNull(ToSql(sqlProvider, unary.Operand, tableSchema, prefix));
                    default:
                        throw new NotSupportedException($"The unary expression type not supported: {unary.Type}");
                }
            case AppSchemaDataFilterBinary binary:
                switch (binary.Type)
                {
                    case LogicType.AndAlso:
                    case LogicType.OrElse:
                    case LogicType.Equal:
                    case LogicType.NotEqual:
                    case LogicType.GreaterThan:
                    case LogicType.GreaterEqual:
                    case LogicType.LessThan:
                    case LogicType.LessEqual:
                        return sqlProvider.Binary(binary.Type,
                            ToSql(sqlProvider, binary.Left, tableSchema, prefix),
                            ToSql(sqlProvider, binary.Right, tableSchema, prefix));
                    case LogicType.Contains:
                        return sqlProvider.In(
                            ToSql(sqlProvider, binary.Right, tableSchema, prefix),
                            ((binary.Left as AppSchemaDataFilterValue)!.Value as IEnumerable<object>)!);
                    case LogicType.NotContains:
                        return sqlProvider.NotIn(
                            ToSql(sqlProvider, binary.Right, tableSchema, prefix),
                            ((binary.Left as AppSchemaDataFilterValue)!.Value as IEnumerable<object>)!);
                    case LogicType.StartsWith:
                        return sqlProvider.LikeStartsWith(
                            ToSql(sqlProvider, binary.Left, tableSchema, prefix),
                            (string)typeof(string).TryConvert((binary.Right as AppSchemaDataFilterValue)?.Value
                                ?? throw new NotSupportedException("The startsWith right value must be string"))!);
                    case LogicType.NotStartsWith:
                        return sqlProvider.NotLikeStartsWith(
                            ToSql(sqlProvider, binary.Left, tableSchema, prefix),
                            (string)typeof(string).TryConvert((binary.Right as AppSchemaDataFilterValue)?.Value
                                ?? throw new NotSupportedException("The notStartsWith right value must be string"))!);
                    case LogicType.EndsWith:
                        return sqlProvider.LikeEndsWith(
                            ToSql(sqlProvider, binary.Left, tableSchema, prefix),
                            (string)typeof(string).TryConvert((binary.Right as AppSchemaDataFilterValue)?.Value
                                ?? throw new NotSupportedException("The endsWith right value must be string"))!);
                    case LogicType.NotEndsWith:
                        return sqlProvider.NotLikeEndsWith(
                            ToSql(sqlProvider, binary.Left, tableSchema, prefix),
                            (string)typeof(string).TryConvert((binary.Right as AppSchemaDataFilterValue)?.Value
                                ?? throw new NotSupportedException("The notEndsWith right value must be string"))!);
                    case LogicType.Match:
                        return sqlProvider.LikeContains(
                            ToSql(sqlProvider, binary.Left, tableSchema, prefix),
                            (string)typeof(string).TryConvert((binary.Right as AppSchemaDataFilterValue)?.Value
                                ?? throw new NotSupportedException("The match right value must be string"))!);
                    case LogicType.NotMatch:
                        return sqlProvider.NotLikeContains(
                            ToSql(sqlProvider, binary.Left, tableSchema, prefix),
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
public class DataSourceExpVisitor : IExpVisitor
{
    /// <inheritdoc />
    public int Priority => EXP_DATA_SOURCE_PRIORITY;

    /// <inheritdoc />
    public async Task<SchemaExp?> VisitExpAsync(CompileContext context, SchemaExp exp)
    {
        if (exp is not FuncCallExp callExp) return null;

        #region Data Source

        // Data source check
        if (callExp.Function.Name == $"{NS_SYSTEM_DATA}.{nameof(SystemData.getdatasource)}")
        {
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

        #endregion

        #region Linq

        // Indicate the source expression
        DataSourceExp? sourceExp = callExp.Args.FirstOrDefault(a => a is DataSourceExp) as DataSourceExp;
        CollectionRootExp? iter = callExp.Args.FirstOrDefault(a => a is CollectionRootExp { Collection: DataSourceExp or FieldAccessExp { Owner: DataSourceExp } }) as CollectionRootExp;
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
                        string fieldName = callExp.Args.ElementAtOrDefault(1) is ConstantExp fieldExp ? fieldExp.Value.ToValue<string>() ?? "" : "";
                        if (string.IsNullOrEmpty(fieldName) || sourceExp.SchemaType is not ArrayType { ElementSchemaType: StructType structType } || structType.GetField(fieldName) == null)
                            throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs);
                        return new FieldsDataSourceExp(sourceExp, fieldName, (await context.GetArrayType(structType.GetField(fieldName)!.SchemeType!))!);
                    }
                
                    // source.length
                    case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.arrlen)}":
                        return new CountDataSourceExp(sourceExp, (await context.GetSchemaTypeAsync(NS_SYSTEM_INT))!);
                
                    // source.OrderBy(field, desc)
                    case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.orderby)}":
                    {
                        string orderField = callExp.Args.ElementAtOrDefault(1) is ConstantExp fieldExp ? fieldExp.Value.ToValue<string>() ?? "" : "";
                        bool descending = callExp.Args.ElementAtOrDefault(2) is ConstantExp descExp && descExp.Value.ToValue<bool>();

                        if (string.IsNullOrEmpty(orderField) || sourceExp.SchemaType is not ArrayType { ElementSchemaType: StructType structType } || structType.GetField(orderField) == null)
                            throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs);
                    
                        return new OrderByDataSourceExp(sourceExp, orderField, descending);
                    }
                
                    // source.skip(n)
                    case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.skip)}":
                        return new SkipDataSourceExp(sourceExp, callExp.Args.ElementAtOrDefault(1)!);
                
                    // source.take(n)
                    case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.take)}":
                        return new TakeDataSourceExp(sourceExp, callExp.Args.ElementAtOrDefault(1)!);
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
                        if (iter.Collection is not DataSourceExp source) return null;

                        string fieldName = callExp.Args.ElementAtOrDefault(1) is ConstantExp fieldExp ? fieldExp.Value.ToValue<string>() ?? "" : "";
                        if (string.IsNullOrEmpty(fieldName) || source.SchemaType is not ArrayType { ElementSchemaType: StructType structType } || structType.GetField(fieldName) == null)
                            throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs);
                        return new FieldsDataSourceExp(source, fieldName, (await context.GetArrayType(structType.GetField(fieldName)!.SchemeType!))!);
                    }

                    // assign
                    case $"{NS_SYSTEM_CONV}.{nameof(SystemConv.assign)}":
                    case $"{NS_SYSTEM_CONV}.{nameof(SystemConv.@default)}":
                    {
                        if (iter.Collection is not FieldAccessExp { Owner: DataSourceExp source } fa || fa.FieldName.Contains('.')) return null;
                        return new FieldsDataSourceExp(source, fa.FieldName, (await context.GetArrayType(((source.SchemaType as ArrayType)!.ElementSchemaType as StructType)!.GetField(fa.FieldName)!.SchemeType!))!);
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
        sourceExp = iter.Collection as DataSourceExp ?? (iter.Collection as FieldAccessExp)?.Owner as DataSourceExp;
        
        // Filter - only support system define functions
        DataSourceRefExp refExp = new DataSourceRefExp(sourceExp!.Source.App, sourceExp.Source.Field, (sourceExp.SchemaType as ArrayType)!.ElementSchemaType!);
        SchemaExp[] refArgs = callExp.Args.Select(a => a == iter
            ? (iter.Collection is FieldAccessExp fldAccess
                ? new FieldAccessExp(refExp, fldAccess.FieldName, fldAccess.SchemaType)
                : refExp)
            : a).ToArray();
        
        // Must be boolean return type
        SchemaExp filterExp = new FuncCallExp(callExp.Function, refArgs, (await context.GetSchemaTypeAsync(NS_SYSTEM_BOOL))!);
        filterExp = await context.VisitSchemaExpAsync(filterExp);
        
        // Must be logic expression
        if (filterExp is not LogicExp logicExp) 
            throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs);

        // Generate filter result
        WhereDataSourceExp filterResult = sourceExp is WhereDataSourceExp whereSource
            ? new WhereDataSourceExp(whereSource.Previous, new BinaryLogicExp(LogicType.AndAlso, whereSource.Filter, logicExp, logicExp.SchemaType))
            : new WhereDataSourceExp(sourceExp, logicExp);

        // Handle other expression types
        return callExp.ExpType switch
        {
            ExpressionType.Filter => filterResult,
            ExpressionType.First => new FirstDataSourceExp(filterResult),
            ExpressionType.Last => new LastDataSourceExp(filterResult),
            ExpressionType.Count => new CountDataSourceExp(filterResult, (await context.GetSchemaTypeAsync(NS_SYSTEM_INT))!),
            ExpressionType.Any => new ExistsDataSourceExp(filterResult, (await context.GetSchemaTypeAsync(NS_SYSTEM_BOOL))!),
            ExpressionType.All => new NoExistsDataSourceExp(filterResult, (await context.GetSchemaTypeAsync(NS_SYSTEM_BOOL))!),
            _ => null
        };

        #endregion
    }

    /// <inheritdoc />
    public async Task<Expression?> CompileExpAsync(CompileContext context, SchemaExp exp, Type expectedType)
    {
        if (exp is not DataSourceExp && exp is not DataResultExp) return null;

        AppSchemaDataResult resultType = AppSchemaDataResult.List;
        string? dataField = null;
        Expression? take = null;
        Expression? skip = null;
        Expression? filter = null;
        List<AppSchemaDataOrder> orders = [];

        // handle source first
        if (exp is DataResultExp dataResultExp)
        {
            resultType = dataResultExp switch
            {
                CountDataSourceExp => AppSchemaDataResult.Count,
                ExistsDataSourceExp => AppSchemaDataResult.Exist,
                NoExistsDataSourceExp => AppSchemaDataResult.NotExist,
                FirstDataSourceExp => AppSchemaDataResult.First,
                LastDataSourceExp => AppSchemaDataResult.Last,
                FieldsDataSourceExp => AppSchemaDataResult.Field,
                _ => resultType
            };
            dataField = (dataResultExp as FieldsDataSourceExp)?.FieldName;
            exp = dataResultExp.Source;
        }

        DataSourceExp? sourceExp = exp as DataSourceExp;
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
                case WhereDataSourceExp whereExp:
                    filter = filter != null 
                        ? Expression.New(typeof(AppSchemaDataFilterBinary).GetConstructors()[0], Expression.Constant(LogicType.AndAlso), filter, await CompileDataSourceFilter(context, whereExp.Filter))
                        : await CompileDataSourceFilter(context, whereExp.Filter);
                    sourceExp = whereExp.Previous;
                    break;
                case OrderByDataSourceExp orderByExp:
                    orders.Add(new AppSchemaDataOrder(orderByExp.OrderField, orderByExp.Descending));
                    sourceExp = orderByExp.Previous;
                    break;
                case TakeDataSourceExp takeExp:
                    take = await context.CompileSchemaExpAsync(takeExp.TakeExp);
                    sourceExp = takeExp.Previous;
                    break;
                case SkipDataSourceExp skipExp:
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

    async Task<Expression> CompileDataSourceFilter(CompileContext context, SchemaExp exp)
    {
        return exp switch
        {
            FieldAccessExp fieldExp => Expression.New(typeof(AppSchemaDataFilterField).GetConstructors()[0], Expression.Constant(fieldExp.FieldName)),
            UnaryLogicExp unaryExp => Expression.New(typeof(AppSchemaDataFilterUnary).GetConstructors()[0], Expression.Constant(unaryExp.Type), await CompileDataSourceFilter(context, unaryExp.Inner)),
            BinaryLogicExp binaryExp => Expression.New(typeof(AppSchemaDataFilterBinary).GetConstructors()[0], Expression.Constant(binaryExp.Type), await CompileDataSourceFilter(context, binaryExp.Left), await CompileDataSourceFilter(context, binaryExp.Right)),
            _ => Expression.New(typeof(AppSchemaDataFilterValue).GetConstructors()[0], await context.CompileSchemaExpAsync(exp)),
        };
    }
}