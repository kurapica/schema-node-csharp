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
public record DataSource(string App, string Field, SchemaExpression? Target, AnySchemeType SchemeType);

/// <summary>
/// The data source expression
/// </summary>
public record DataSourceExpression(DataSource Source) : SchemaExpression(Source.SchemeType);

/// <summary>
/// The filter data source expression
/// </summary>
public record WhereDataSourceExpression(DataSourceExpression Previous, SchemaExpression FilterExp) : DataSourceExpression(Previous.Source);

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
public record CountDataSourceExpression(DataSourceExpression Source, AnySchemeType SchemeType) : SchemaExpression(SchemeType);

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
public record FieldsDataSourceExpression(DataSourceExpression Source, string FieldName, AnySchemeType SchemeType) : SchemaExpression(SchemeType);

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

        switch (callExp.ExpType)
        {
            case ExpressionType.Call:
            {
                DataSourceExpression? sourceExp = callExp.Args.FirstOrDefault(a => a is DataSourceExpression) as DataSourceExpression;
                if (sourceExp == null) return null;
            
                switch (callExp.Function.Name)
                {
                    // getfields(source)
                    case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.getfields)}":
                    {
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
            case ExpressionType.Map:
                break;
            case ExpressionType.Reduce:
                return null;
        }
        
        // Filter for other exp types
        int index = Array.FindIndex(callExp.Args, a => a is IteratorExpression { Array: DataSourceExpression or FieldAccessExpression { Owner: DataSourceExpression } });
        if (index < 0) return null;
        IteratorExpression iter = (callExp.Args[index] as IteratorExpression)!;
        
        
        
        // Linq data source access expression
        switch (callExp.ExpType)
        {
            case ExpressionType.First:
            case ExpressionType.Last:
            case ExpressionType.Filter:
            case ExpressionType.Count:
            case ExpressionType.Any:
            case ExpressionType.All:
            {
                break;
            }
        }
        
        #endregion
        
        return null;
    }
}