using Microsoft.Extensions.Logging;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Utility;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json.Nodes;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Components;

public static class AppDataQueryExtension
{
    #region Entity Query

    /// <summary>
    /// Gets the entity data by full primary keys
    /// </summary>
    public static async Task<T?> GetEntityAsync<T>(this SchemaContext context, string target, params object[] keys)
    {
        (AppFieldType appFieldType, IReadOnlyList<PropertyInfo>? primaries) = await context.AssertAppField<T>();
        AppSchemaDataFilter? filter = null;
        if (primaries is { Count: > 0 })
        {
            if (keys.Length != primaries.Count) throw new ArgumentException($"The type {typeof(T).FullName} primary key count not match");
           
            for (int i = 0; i < keys.Length; i++)
            {
                JsonValue? val = JsonValue.Create(keys[i]);
                if (val == null || val.IsEmpty()) throw new ArgumentException($"The value for primary {primaries[i].Name} is not valid");

                var keyFilter = new AppSchemaDataFilterBinary(LogicType.Equal,
                    new AppSchemaDataFilterField(primaries[i].Name.ToCamelCase()),
                    new AppSchemaDataFilterValue(val));
                filter = filter == null ? keyFilter : filter.AndAlso(keyFilter);
            }
        }

        var (value, _) = await context.GetFieldDataAsync(appFieldType, target, AppSchemaDataResult.First, filter);
        return value != null ? value.ToValue<T>() : default;
    }

    /// <summary>
    /// Gets the entity data by full primary keys
    /// </summary>
    public static async Task<T?> GetEntityAsync<T>(this SchemaContext context, string target, Expression<Func<T, bool>> cond, bool forUpdate = false)
    {
        (AppFieldType appFieldType, _) = await context.AssertAppField<T>();
        
        // first only, no more check
        AppSchemaDataFilter filter = AppSchemaDataFilterVisitor.Build(cond);
        var (value, _) = await context.GetFieldDataAsync(appFieldType, target, AppSchemaDataResult.First, filter, forUpdate: forUpdate);
        return value != null ? value.ToValue<T>() : default;
    }

    /// <summary>
    /// Gets the entity data by full primary keys
    /// </summary>
    public static async Task<List<T>> GetEntitiesAsync<T>(this SchemaContext context, string target, Expression<Func<T, bool>> cond, bool forUpdate = false)
    {
        (AppFieldType appFieldType, _) = await context.AssertAppField<T>();
        AppSchemaDataFilter filter = AppSchemaDataFilterVisitor.Build(cond);
        var (value, _) = await context.GetFieldDataAsync(appFieldType, target, AppSchemaDataResult.List, filter, forUpdate: forUpdate);
        return value is ArrayTypeNode arr ? arr.Select(o => o.ToValue<T>()!).ToList() : [];
    }

    /// <summary>
    /// Gets the entity data by full primary keys
    /// </summary>
    public static async Task<(List<T> value, int total)> GetEntitiesAsync<T>(this SchemaContext context, string target, Expression<Func<T, bool>> cond, int take, int skip = 0, bool desc = false, AppSchemaDataOrder[]? orderBy = null, bool forUpdate = false)
    {
        (AppFieldType appFieldType, _) = await context.AssertAppField<T>();
        AppSchemaDataFilter filter = AppSchemaDataFilterVisitor.Build(cond);
        var (value, total) = await context.GetFieldDataAsync(appFieldType, target, AppSchemaDataResult.List, filter, skip, take, desc, orderBy, forUpdate: forUpdate);
        return (value is ArrayTypeNode arr ? arr.Select(o => o.ToValue<T>()!).ToList() : [], total);
    }

    /// <summary>
    /// Gets the entity data by full primary keys
    /// </summary>
    public static async Task<(List<T> value, int total)> GetFieldEntitiesAsync<T>(this SchemaContext context, AppFieldType field, string target, Expression<Func<T, bool>> cond, int skip = 0, int take = 0, bool desc = false, AppSchemaDataOrder[]? orderBy = null, bool forUpdate = false)
    {
        context.AssertType<T>(field);
        AppSchemaDataFilter filter = AppSchemaDataFilterVisitor.Build(cond);
        var (value, total) = await context.GetFieldDataAsync(field, target, AppSchemaDataResult.List, filter, skip, take, desc, orderBy, forUpdate: forUpdate);
        return (value is ArrayTypeNode arr ? arr.Select(o => o.ToValue<T>()!).ToList() : [], total);
    }

    #endregion

    /// <summary>
    /// Gets the field data
    /// </summary>
    public static async Task<(AnySchemaNode? value, int total)> GetFieldDataAsync(this SchemaContext context, AppFieldType field, 
        string target, JsonNode? filter = null, int skip = 0, int take = 0, bool desc = false, AppSchemaDataOrder[]? orderBy = null, 
        bool forUpdate = false, bool genDisplayOnly = false)
    {
        // Front end only
        if ((field.Frontend ?? false) || (field.Disable ?? false)) return (null, 0);

        var dataProvider = context.GetService<IAppDataProvider>();
        if (dataProvider == null) throw new InvalidOperationException(APP_DATA_PROVIDER_NOT_EXIST);

        (AppFieldType? sourceField, target) = await context.GetSourceFieldNode(field, target);
        if (sourceField == null) return (null, 0);
        field = sourceField;

        DynamicTableSchema schema = await context.PrepareFieldDataAsync(field);

        try
        {
            (AnySchemaNode? result, int total) = await dataProvider.QueryDynamicTableAsync(schema, target, filter, skip, take, desc, orderBy, forUpdate);

            // Generate display only fields
            if (genDisplayOnly)
                await schema.GenerateDisplayOnlyFields(context, result);

            return (result, total);
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Gets the field data
    /// </summary>
    public static async Task<(AnySchemaNode? value, int total)> GetFieldDataAsync(this SchemaContext context,
        AppFieldType field, string target, AppSchemaDataResult type, AppSchemaDataFilter? filter,
        int skip = 0, int take = 0, bool desc = false, AppSchemaDataOrder[]? orderBy = null, string? dataField = null, 
        bool forUpdate = false, bool genDisplayOnly = false)
    {
        // Front end only
        if ((field.Frontend ?? false) || (field.Disable ?? false)) return (null, 0);

        var dataProvider = context.GetService<IAppDataProvider>();
        if (dataProvider == null) throw new InvalidOperationException(APP_DATA_PROVIDER_NOT_EXIST);

        (AppFieldType? sourceField, target) = await context.GetSourceFieldNode(field, target);
        if (sourceField == null) return (null, 0);
        field = sourceField;

        DynamicTableSchema schema = await context.PrepareFieldDataAsync(field);

        try
        {
            (AnySchemaNode? result, int total) = await dataProvider
                .QueryDynamicTableAsync(schema, target, type, filter, skip, take, desc, orderBy, dataField, forUpdate);

            // Generate display only fields
            if (genDisplayOnly)
                await schema.GenerateDisplayOnlyFields(context, result);

            return (result, total);
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Gets the filter field data for data source compile expression
    /// </summary>
    public static async Task<AnySchemaNode?> GetSchemaDataAsync(
        this SchemaContext context, string app, string field, string target, AppSchemaDataResult type, AppSchemaDataFilter? filter, 
        int skip = 0, int take = 0, bool desc = false, AppSchemaDataOrder[]? orderBy = null, string? dataField = null)
    {
        AppType? appType = await context.GetAppTypeAsync(app);
        AppFieldType? appField = appType?.GetField(field);
        if (appField == null) return null;
        
        // Validate and transform filter
        bool isValidFilter = filter == null;
        if (filter != null)
        {
            isValidFilter = filter.Transform(out AppSchemaDataFilter? final);
            filter = final;

            // Avoid invalid filter types like false means no data
            if (isValidFilter && filter is AppSchemaDataFilterValue or AppSchemaDataFilterField)
                isValidFilter = false;
        }

        if (string.IsNullOrEmpty(target))
            target = context.GetSchemaContextItem<Access>()?.Target ?? string.Empty;
        
        if (!isValidFilter || string.IsNullOrEmpty(target)) return type switch
        {
            AppSchemaDataResult.Count => SchemaContext.SystemInt.CreateNode(0),
            AppSchemaDataResult.Exist => SchemaContext.SystemBool.CreateNode(false),
            AppSchemaDataResult.First => null,
            AppSchemaDataResult.Last => null,
            AppSchemaDataResult.Field => new ArrayTypeNode(((appField.SchemaType as ArrayType)!.ElementSchemaType as StructType)!.GetField(dataField!)!.SchemeType!),
            _ => new ArrayTypeNode(appField.SchemaType!)
        };

        (AnySchemaNode? res, _) = await context.GetFieldDataAsync(appField, target, type, filter, skip, take, desc, orderBy, dataField);
        return res;
    }
}
