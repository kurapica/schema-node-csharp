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
        (AppFieldType appFieldType, IReadOnlyList<PropertyInfo>? primarys) = await context.AssertAppField<T>();
        if (primarys == null) throw new ArgumentException($"The app field of type {typeof(T).FullName} only support single value");

        if (keys.Length != primarys.Count) throw new ArgumentException($"The type {typeof(T).FullName} primary key count not match");

        JsonObject query = [];
        for (int i = 0; i < keys.Length; i++)
        {
            query[primarys[i].Name.ToCamelCase()] = JsonValue.Create(keys[i]);
        }

        (List<T> result, _) = await GetFieldEntitiesAsync<T>(context, appFieldType, target, query, take: 1);
        return result is { Count: > 0 } ? result[0] : default;
    }

    /// <summary>
    /// Gets the entity data by full primary keys
    /// </summary>
    public static async Task<T?> GetEntityAsync<T>(this SchemaContext context, string target, Expression<Func<T, bool>> cond, bool forUpdate = false)
    {
        (AppFieldType appFieldType, IReadOnlyList<PropertyInfo>? primarys) = await context.AssertAppField<T>();
        if (primarys == null) throw new ArgumentException($"The app field of type {typeof(T).FullName} only support single value");

        EntityConditionVisitor visitor = new();
        visitor.Visit(cond);
        JsonNode filter = visitor.Condition;
        if (filter is not JsonObject obj) throw new ArgumentException("The condition is not valid");

        JsonObject query = [];
        foreach (PropertyInfo t in primarys)
        {
            string key = t.Name.ToCamelCase();
            if (obj.TryGetPropertyValue(key, out JsonNode? val) && val is JsonValue v && !v.IsEmpty())
                query[key] = v.DeepClone();
            else
                throw new ArgumentException("The condition is not valid");
        }

        (List<T> result, _) = await GetFieldEntitiesAsync<T>(context, appFieldType, target, query, take: 1, forUpdate: forUpdate);
        return result is { Count: > 0 } ? result[0] : default;
    }

    /// <summary>
    /// Gets the entity data by full primary keys
    /// </summary>
    public static async Task<List<T>> GetEntitiesAsync<T>(this SchemaContext context, string target, Expression<Func<T, bool>> cond, bool forUpdate = false)
    {
        (AppFieldType appFieldType, IReadOnlyList<PropertyInfo>? primarys) = await context.AssertAppField<T>();
        if (primarys == null) throw new ArgumentException($"The app field of type {typeof(T).FullName} only support single value");

        EntityConditionVisitor visitor = new();
        visitor.Visit(cond);
        JsonNode filter = visitor.Condition;
        if (filter is not JsonObject obj) throw new ArgumentException("The condition is not valid");

        (List<T> result, _) = await GetFieldEntitiesAsync<T>(context, appFieldType, target, obj, forUpdate: forUpdate);
        return result;
    }

    /// <summary>
    /// Gets the entity data by full primary keys
    /// </summary>
    public static async Task<(List<T> value, int total)> GetEntitiesAsync<T>(this SchemaContext context, string target, Expression<Func<T, bool>> cond, int take, int skip = 0, bool desc = false, AppSchemaDataOrder[]? orderBy = null, bool forUpdate = false)
    {
        (AppFieldType appFieldType, IReadOnlyList<PropertyInfo>? primarys) = await context.AssertAppField<T>();
        if (primarys == null) throw new ArgumentException($"The app field of type {typeof(T).FullName} only support single value");

        EntityConditionVisitor visitor = new();
        visitor.Visit(cond);
        JsonNode filter = visitor.Condition;
        if (filter is not JsonObject obj) throw new ArgumentException("The condition is not valid");

        return await GetFieldEntitiesAsync<T>(context, appFieldType, target, obj, skip, take, desc, orderBy, forUpdate);
    }

    /// <summary>
    /// Gets the entity data by full primary keys
    /// </summary>
    public static async Task<(List<T> value, int total)> GetFieldEntitiesAsync<T>(this SchemaContext context, AppFieldType field, string target, Expression<Func<T, bool>> cond, int skip = 0, int take = 0, bool desc = false, AppSchemaDataOrder[]? orderBy = null, bool forUpdate = false)
    {
        context.AssertType<T>(field);

        EntityConditionVisitor visitor = new();
        visitor.Visit(cond);
        JsonNode filter = visitor.Condition;
        if (filter is not JsonObject obj || obj.IsEmpty()) throw new ArgumentException("The condition is not valid");

        return await GetFieldEntitiesAsync<T>(context, field, target, obj, skip, take, desc, orderBy, forUpdate);
    }

    /// <summary>
    /// Gets the entity data
    /// </summary>
    public static async Task<(List<T> value, int total)> GetFieldEntitiesAsync<T>(this SchemaContext context, AppFieldType field, string target, JsonNode filter, int skip = 0, int take = 0, bool desc = false, AppSchemaDataOrder[]? orderBy = null, bool forUpdate = false)
    {
        context.AssertType<T>(field);

        (AnySchemaNode? result, int total) = await GetFieldDataAsync(context, field, target, filter, skip, take, desc, orderBy, forUpdate);
        List<T> results = [];
        if (result is ArrayTypeNode arr)
        {
            foreach (AnySchemaNode item in arr)
            {
                if (item is StructTypeNode obj)
                {
                    T? val = obj.ToValue<T>();
                    if (val != null) results.Add(val);
                }
            }
        }
        else if (result is StructTypeNode obj)
        {
            T? val = obj.ToValue<T>();
            if (val != null) results.Add(val);
        }
        return (results, total);
    }

    #endregion

    /// <summary>
    /// Gets the field data
    /// </summary>
    public static async Task<(AnySchemaNode? value, int total)> GetFieldDataAsync(this SchemaContext context, AppFieldType field, string target, JsonNode? filter = null, int skip = 0, int take = 0, bool desc = false, AppSchemaDataOrder[]? orderBy = null, bool forUpdate = false)
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
        AppFieldType field, string target, AccessExpNode? filter, int skip = 0, int take = 0, bool desc = false, 
        AppSchemaDataOrder[]? orderBy = null, bool forUpdate = false, bool onlyCount = false)
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
                .QueryDynamicTableAsync(schema, target, filter, skip, take, desc, orderBy, forUpdate, onlyCount);

            // Generate display only fields
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
        int skip = 0, int take = 0, bool? desc = false, AppSchemaDataOrder[]? orderBy = null, string? dataField = null)
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
                .QueryDynamicTableAsync(context, schema, target, type, filter, skip, take, desc, orderBy, dataField);

            // Generate display only fields
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
    /// Gets the filter field data
    /// </summary>
    public static async Task<AnySchemaNode?> GetFilterFieldDataAsync(
        this SchemaContext context, string app, string field, string target, AccessExpNode? filter, AppDataSourceAccessResult type, 
        int skip = 0, int take = 0, AppSchemaDataOrder[]? orderBy = null)
    {
        AppType? appType = await context.GetAppTypeAsync(app);
        AppFieldType? appField = appType?.GetField(field);
        if (appField == null) return null;

        if (string.IsNullOrEmpty(target))
        {
            target = context.GetSchemaContextItem<Access>()?.Target ?? string.Empty;
            if (string.IsNullOrEmpty(target)) return type switch
            {
                AppDataSourceAccessResult.Count => (await context.GetSchemaTypeAsync(NS_SYSTEM_INT))!.CreateNode(0),
                AppDataSourceAccessResult.First => null,
                AppDataSourceAccessResult.Last => null,
                _ => new ArrayTypeNode(appField.SchemaType!)
            };
        }

        (AnySchemaNode? res, int total) = await context.GetFieldDataAsync(appField, target, filter, skip, 
            type is AppDataSourceAccessResult.First or AppDataSourceAccessResult.Last ? 1 : take,
            false, orderBy, false, type == AppDataSourceAccessResult.Count);
        return type switch
        {
            AppDataSourceAccessResult.Count => (await context.GetSchemaTypeAsync(NS_SYSTEM_INT))!.CreateNode(total),
            AppDataSourceAccessResult.First => res is ArrayTypeNode array ? array.FirstOrDefault() : res,
            AppDataSourceAccessResult.Last => res is ArrayTypeNode array ? array.LastOrDefault() : res,
            _ => res
        };
    }

    /// <summary>
    /// Gets the filter field data
    /// </summary>
    public static async Task<AnySchemaNode?> GetSchemaDataAsync(
        this SchemaContext context, string app, string field, string target, AppSchemaDataResult type, AppSchemaDataFilter? filter, 
        int skip = 0, int take = 0, AppSchemaDataOrder[]? orderBy = null, string? dataField = null)
    {
        AppType? appType = await context.GetAppTypeAsync(app);
        AppFieldType? appField = appType?.GetField(field);
        if (appField == null) return null;

        if (string.IsNullOrEmpty(target))
        {
            target = context.GetSchemaContextItem<Access>()?.Target ?? string.Empty;
            if (string.IsNullOrEmpty(target)) return type switch
            {
                AppSchemaDataResult.Count => (await context.GetSchemaTypeAsync(NS_SYSTEM_INT))!.CreateNode(0),
                AppSchemaDataResult.Exist => (await context.GetSchemaTypeAsync(NS_SYSTEM_BOOL))!.CreateNode(false),
                AppSchemaDataResult.NotExist => (await context.GetSchemaTypeAsync(NS_SYSTEM_BOOL))!.CreateNode(true),
                AppSchemaDataResult.First => null,
                AppSchemaDataResult.Last => null,
                AppSchemaDataResult.Field => new ArrayTypeNode(((appField.SchemaType as ArrayType)!.ElementSchemaType as StructType)!.GetField(dataField!)!.SchemeType!),
                _ => new ArrayTypeNode(appField.SchemaType!)
            };
        }

        (AnySchemaNode? res, _) = await context.GetFieldDataAsync(appField, target, type, filter, skip, take, orderBy, dataField);
        return res;
    }
}
