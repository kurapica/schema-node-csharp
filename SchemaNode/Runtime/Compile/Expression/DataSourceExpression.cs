using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Function;
using static SchemaNode.Utility.Constant;
using ExpressionType = SchemaNode.Enum.ExpressionType;

// ReSharper disable NotAccessedPositionalProperty.Global

namespace SchemaNode.Runtime;

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

/// <summary>
/// The count data source expression
/// </summary>
public record CountDataSourceExpression(DataSourceExpression Source, AnySchemaType SchemaType) : SchemaExpression(SchemaType);

/// <summary>
/// Exists data source expression
/// </summary>
public record ExistsDataSourceExpression(DataSourceExpression Source, AnySchemaType SchemaType) : SchemaExpression(SchemaType);

/// <summary>
/// No exists data source expression
/// </summary>
public record NoExistsDataSourceExpression(DataSourceExpression Source, AnySchemaType SchemaType) : SchemaExpression(SchemaType);

/// <summary>
/// The first data source expression
/// </summary>
public record FirstDataSourceExpression(DataSourceExpression Source) : SchemaExpression((Source.SchemaType as ArrayType)!.ElementSchemaType!);

/// <summary>
/// The last data source expression
/// </summary>
public record LastDataSourceExpression(DataSourceExpression Source) : SchemaExpression((Source.SchemaType as ArrayType)!.ElementSchemaType!);

/// <summary>
/// The field access data source expression
/// </summary>
public record FieldsDataSourceExpression(DataSourceExpression Source, string FieldName, AnySchemaType SchemaType) : SchemaExpression(SchemaType);

/// <summary>
/// The data source visitor
/// </summary>
public class DataSourceVisitor : IExpressionVisitor
{
    /// <inheritdoc />
    public int Priority => EXP_DATA_SOURCE_PRIORITY;

    /// <inheritdoc />
    public SchemaExpression? VisitExpression(SchemaContext context, SchemaExpression exp)
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
            AppType? appType = context.GetAppTypeAsync(app).GetAwaiter().GetResult();
            AppFieldType? appField = appType?.GetField(field);
            return appField is { SchemaType: ArrayType { ElementSchemaType: StructType structType, Primary: { Length: > 0 } } }
                ? new DataSourceExpression(new DataSource(app, field, callExp.Args.ElementAtOrDefault(2), structType))
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
                    // getfields(source)
                    case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.getfields)}":
                    {
                        // not support a.b.c deep field access
                        string fieldName = callExp.Args.ElementAtOrDefault(1) is ConstantExpression fieldExp ? fieldExp.Value.ToValue<string>() ?? "" : "";
                        if (string.IsNullOrEmpty(fieldName) || sourceExp.SchemaType is not ArrayType { ElementSchemaType: StructType structType } || structType.GetField(fieldName) == null)
                            throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);
                        return new FieldsDataSourceExpression(sourceExp, fieldName, context.GetArraySchemaTypeAsync(structType.GetField(fieldName)!.SchemeType!).GetAwaiter().GetResult()!);
                    }
                
                    // source.length
                    case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.arrlen)}":
                        return new CountDataSourceExpression(sourceExp, context.GetSchemaTypeAsync(NS_SYSTEM_INT).GetAwaiter().GetResult()!);
                
                    // source.orderby(field, desc)
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
                    // getfield(source, field), conver the case to FieldsDataSourceExpression
                    case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.getfield)}":
                    case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.getfielddefault)}":
                    {
                        if (iter.Array is not DataSourceExpression source) return null;

                        string fieldName = callExp.Args.ElementAtOrDefault(1) is ConstantExpression fieldExp ? fieldExp.Value.ToValue<string>() ?? "" : "";
                        if (string.IsNullOrEmpty(fieldName) || source.SchemaType is not ArrayType { ElementSchemaType: StructType structType } || structType.GetField(fieldName) == null)
                            throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);
                        return new FieldsDataSourceExpression(source, fieldName, context.GetArraySchemaTypeAsync(structType.GetField(fieldName)!.SchemeType!).GetAwaiter().GetResult()!);
                    }

                    // assign
                    case $"{NS_SYSTEM_CONV}.{nameof(SystemConv.assign)}":
                    case $"{NS_SYSTEM_CONV}.{nameof(SystemConv.@default)}":
                    {
                        if (iter.Array is not FieldAccessExpression { Owner: DataSourceExpression source } fa || fa.FieldName.Contains('.')) return null;
                        return new FieldsDataSourceExpression(source, fa.FieldName, context.GetArraySchemaTypeAsync(((source.SchemaType as ArrayType)!.ElementSchemaType as StructType)!.GetField(fa.FieldName)!.SchemeType!).GetAwaiter().GetResult()!);
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
        SchemaExpression filterExp = new FuncCallExpression(callExp.Function, refArgs, context.GetSchemaTypeAsync(NS_SYSTEM_BOOL).GetAwaiter().GetResult()!);
        filterExp = context.VisitSchemaExpression(filterExp);
        
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
            ExpressionType.First => new FirstDataSourceExpression(filterResult),
            ExpressionType.Last => new LastDataSourceExpression(filterResult),
            ExpressionType.Filter => filterResult,
            ExpressionType.Count => new CountDataSourceExpression(filterResult,context.GetSchemaTypeAsync(NS_SYSTEM_INT).GetAwaiter().GetResult()!),
            ExpressionType.Any => new ExistsDataSourceExpression(filterResult, context.GetSchemaTypeAsync(NS_SYSTEM_BOOL).GetAwaiter().GetResult()!),
            ExpressionType.All => new NoExistsDataSourceExpression(filterResult,context.GetSchemaTypeAsync(NS_SYSTEM_BOOL).GetAwaiter().GetResult()!),
            _ => null
        };

        #endregion
    }
}