using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json.Nodes;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Utility;

namespace SchemaNode.Data;

/// <summary>
/// The entity data operations
/// </summary>
public static class EntityExtension
{
    #region Query

    /// <summary>
    /// Gets the entity data by full primary keys
    /// </summary>
    public static async Task<T?> GetEntityAsync<T>(this SchemaContext context, string? target, params object[] keys)
    {
        AppFieldType appFieldType = await context.AssertAppField<T>();
        if (appFieldType.Application.ScopeType != AppScopeType.SystemLevel && string.IsNullOrEmpty(target))
            return default;
        
        AppSchemaDataFilter? filter = null;
        var primaries = appFieldType.GetPrimaryProperties();
        if (primaries.Any())
        {
            if (keys.Length != primaries.Count) throw new ArgumentException($"The type {typeof(T).FullName} primary key count not contains");
           
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
        
        using var _ = context.StackAccess(appFieldType.App, target);
        var (value, _) = await context.GetAppFieldDataAsync(appFieldType, AppSchemaDataResult.First, filter);
        return value != null ? value.GetValue<T>() : default;
    }

    /// <summary>
    /// Gets the entity data by full primary keys
    /// </summary>
    public static async Task<T?> GetEntityAsync<T>(this SchemaContext context, string? target, Expression<Func<T, bool>> cond, bool forUpdate = false)
    {
        AppFieldType appFieldType = await context.AssertAppField<T>();
        if (appFieldType.Application.ScopeType != AppScopeType.SystemLevel && string.IsNullOrEmpty(target))
            return default;
        
        // first only, no more check
        AppSchemaDataFilter filter = AppSchemaDataFilterVisitor.Build(cond);
       
        using var stack = context.StackAccess(appFieldType.App, target);
        var (value, _) = await context.GetAppFieldDataAsync(appFieldType, AppSchemaDataResult.First, filter,
            forUpdate: forUpdate);
        return value != null ? value.GetValue<T>() : default;
    }

    /// <summary>
    /// Gets the entity data by full primary keys
    /// </summary>
    public static async Task<List<T>> GetEntitiesAsync<T>(this SchemaContext context, string? target, Expression<Func<T, bool>> cond, bool forUpdate = false)
    {
        AppFieldType appFieldType = await context.AssertAppField<T>();
        if (appFieldType.Application.ScopeType != AppScopeType.SystemLevel && string.IsNullOrEmpty(target))
            return [];
        
        AppSchemaDataFilter filter = AppSchemaDataFilterVisitor.Build(cond);
        
        using var stack = context.StackAccess(appFieldType.App, target);
        var (value, _) = await context.GetAppFieldDataAsync(appFieldType, AppSchemaDataResult.List, 
            filter, forUpdate: forUpdate);
        return value is ArrayNode arr ? arr.Select(o => o.GetValue<T>()!).ToList() : [];
    }

    /// <summary>
    /// Gets the entity data by full primary keys
    /// </summary>
    public static async Task<(List<T> value, int total)> GetEntitiesAsync<T>(this SchemaContext context, string? target, Expression<Func<T, bool>> cond, int take, int skip = 0, bool desc = false, AppSchemaDataOrder[]? orderBy = null, bool forUpdate = false)
    {
        AppFieldType appFieldType = await context.AssertAppField<T>();
        if (appFieldType.Application.ScopeType != AppScopeType.SystemLevel && string.IsNullOrEmpty(target))
            return ([], 0);
        
        AppSchemaDataFilter filter = AppSchemaDataFilterVisitor.Build(cond);
        using var stack = context.StackAccess(appFieldType.App, target);
        
        var (value, total) = await context.GetAppFieldDataAsync(appFieldType, AppSchemaDataResult.List, filter,
            skip, take, desc, orderBy, forUpdate: forUpdate);
        return (value is ArrayNode arr ? arr.Select(o => o.GetValue<T>()!).ToList() : [], total);
    }

    /// <summary>
    /// Gets the entity data by full primary keys
    /// </summary>
    public static async Task<(List<T> value, int total)> GetFieldEntitiesAsync<T>(this SchemaContext context, AppFieldType field, string? target, Expression<Func<T, bool>> cond, int skip = 0, int take = 0, bool desc = false, AppSchemaDataOrder[]? orderBy = null, bool forUpdate = false)
    {
        context.AssertType<T>(field);
        if (field.Application.ScopeType != AppScopeType.SystemLevel && string.IsNullOrEmpty(target))
            return ([], 0);
        
        AppSchemaDataFilter filter = AppSchemaDataFilterVisitor.Build(cond);
        
        using var stack = context.StackAccess(field.App, target);
    
        var (value, total) = await context.GetAppFieldDataAsync(field, AppSchemaDataResult.List, filter, skip, take,
            desc, orderBy, forUpdate: forUpdate);
        return (value is ArrayNode arr ? arr.Select(o => o.GetValue<T>()!).ToList() : [], total);
    }

    #endregion

    #region Delete

    /// <summary>
    /// Delete entity data
    /// </summary>
    public static async Task<bool> DeleteEntityAsync<T>(this SchemaContext context, string? target, T value)
    {
        AppFieldType appFieldType = await context.AssertAppField<T>();
        if (appFieldType.Application.ScopeType != AppScopeType.SystemLevel && string.IsNullOrWhiteSpace(target))
            throw new ArgumentException($"The target is required for app field of type {typeof(T).FullName}");
        
        using var _ = context.StackAccess(appFieldType.App, target);
        if (!appFieldType.GetPrimaryProperties().Any())  return await context.SaveFieldDataAsync(appFieldType, null);

        var node = (appFieldType.ValueType as ArrayType)?.Element?.From(value) ?? throw new ArgumentException($"The value of type {typeof(T).FullName} is invalid for delete");
        return await context.DeleteFieldListDataAsync(appFieldType, node);
    }

    /// <summary>
    /// Delete entity data
    /// </summary>
    public static async Task<bool> DeleteEntityAsync<T>(this SchemaContext context, string? target, params object[] keys)
    {
        AppFieldType appFieldType = await context.AssertAppField<T>();
        if (appFieldType.Application.ScopeType != AppScopeType.SystemLevel && string.IsNullOrWhiteSpace(target))
            throw new ArgumentException($"The target is required for app field of type {typeof(T).FullName}");
        
        using var _ = context.StackAccess(appFieldType.App, target);
        var primaries = appFieldType.GetPrimaryProperties();
        if (primaries == null || primaries.Count == 0)
            return await context.SaveFieldDataAsync(appFieldType, null);

        if (keys.Length != primaries.Count)
            throw new ArgumentException($"The type {typeof(T).FullName} primary key count not contains");
        
        AppSchemaDataFilter? filter = null;
        for (int i = 0; i < keys.Length; i++)
        {
            AppSchemaDataFilterBinary keyFilter = new AppSchemaDataFilterBinary(LogicType.Equal,
                new AppSchemaDataFilterField(primaries[i].Name.ToCamelCase()),
                new AppSchemaDataFilterValue(keys[i]));
            filter = filter != null
                ? new AppSchemaDataFilterBinary(LogicType.AndAlso, filter, keyFilter)
                : keyFilter;
        }

        if (filter == null) throw new ArgumentException($"The type {typeof(T).FullName} primary key is invalid");
        return await context.DeleteFieldListDataAsync(appFieldType, filter);
    }

    /// <summary>
    /// Delete entity data
    /// </summary>
    public static async Task<bool> DeleteEntitiesAsync<T>(this SchemaContext context, string? target, List<T> value)
    {
        AppFieldType appFieldType = await context.AssertAppField<T>();
        var primaries = appFieldType.GetPrimaryProperties();
        if (primaries == null) throw new ArgumentException($"The app field of type {typeof(T).FullName} only support single value");
        if (appFieldType.Application.ScopeType != AppScopeType.SystemLevel && string.IsNullOrWhiteSpace(target))
            throw new ArgumentException($"The target is required for app field of type {typeof(T).FullName}");

        using var _ = context.StackAccess(appFieldType.App, target);
        
        var array = new ArrayNode(appFieldType.ValueType!);
        foreach (T valueItem in value)
        {
            var node = (appFieldType.ValueType as ArrayType)?.Element?.From(valueItem) ?? throw new ArgumentException($"The value of type {typeof(T).FullName} is invalid for delete");
            array.Add(node);
        }

        return await context.DeleteFieldListDataAsync(appFieldType, array);
    }

    /// <summary>
    /// Delete entity data
    /// </summary>
    public static async Task<bool> DeleteEntitiesAsync<T>(this SchemaContext context, string? target, Expression<Func<T, bool>> cond)
    {
        AppFieldType appFieldType = await context.AssertAppField<T>();
        return await context.DeleteFieldEntityAsync(appFieldType, target, cond);
    }

    /// <summary>
    /// Delete entity data
    /// </summary>
    public static async Task<bool> DeleteFieldEntityAsync<T>(this SchemaContext context, AppFieldType field, string target, T value)
    {
        context.AssertType<T>(field);
        if (field.Application.ScopeType != AppScopeType.SystemLevel && string.IsNullOrWhiteSpace(target))
            throw new ArgumentException($"The target is required for app field of type {typeof(T).FullName}");
        using var _ = context.StackAccess(field.App, target);
        var node = (field.ValueType as ArrayType)?.Element?.From(value) ?? throw new ArgumentException($"The value of type {typeof(T).FullName} is invalid for delete");
        return await context.DeleteFieldListDataAsync(field, node);
    }

    /// <summary>
    /// Delete entity data
    /// </summary>
    public static async Task<bool> DeleteFieldEntityAsync<T>(this SchemaContext context, AppFieldType field, string target, List<T> value)
    {
        context.AssertType<T>(field);
        if (field.Application.ScopeType != AppScopeType.SystemLevel && string.IsNullOrWhiteSpace(target))
            throw new ArgumentException($"The target is required for app field of type {typeof(T).FullName}");
        
        using var _ = context.StackAccess(field.App, target);
        var array = new ArrayNode(field.ValueType!);
        foreach (T valueItem in value)
        {
            var node = (field.ValueType as ArrayType)?.Element?.From(valueItem) ?? throw new ArgumentException($"The value of type {typeof(T).FullName} is invalid for delete");
            array.Add(node);
        }
        return await context.DeleteFieldListDataAsync(field, array);
    }

    /// <summary>
    /// Delete entity data
    /// </summary>
    public static Task<bool> DeleteFieldEntityAsync<T>(this SchemaContext context, AppFieldType field, string? target, Expression<Func<T, bool>> cond)
    {
        context.AssertType<T>(field);
        if (field.Application.ScopeType != AppScopeType.SystemLevel && string.IsNullOrWhiteSpace(target))
            throw new ArgumentException($"The target is required for app field of type {typeof(T).FullName}");
        
        using var _ = context.StackAccess(field.App, target);
        AppSchemaDataFilter filter = AppSchemaDataFilterVisitor.Build(cond);
        return context.DeleteFieldListDataAsync(field, filter);
    }

    #endregion
    
    #region Update
    
    /// <summary>
    /// Save entity data
    /// </summary>
    public static async Task<bool> SaveEntityAsync<T>(this SchemaContext context, string? target, T value)
    {
        AppFieldType appFieldType = await context.AssertAppField<T>();
        if (appFieldType.Application.ScopeType != AppScopeType.SystemLevel && string.IsNullOrWhiteSpace(target))
            throw new ArgumentException($"The target is required for app field of type {typeof(T).FullName}");
        
        using var stack = context.StackAccess(appFieldType.App, target);
        return await context.SaveFieldDataAsync(appFieldType, appFieldType.ValueType!.From(value));
    }

    /// <summary>
    /// Save entity list data
    /// </summary>
    public static async Task<bool> SaveEntitiesAsync<T>(this SchemaContext context, string? target, List<T> values)
    {
        AppFieldType appFieldType = await context.AssertAppField<T>();
        var primaries = appFieldType.GetPrimaryProperties();
        if (primaries == null) throw new ArgumentException($"The app field of {typeof(T).FullName} only support single value");
        if (appFieldType.Application.ScopeType != AppScopeType.SystemLevel && string.IsNullOrWhiteSpace(target))
            throw new ArgumentException($"The target is required for app field of type {typeof(T).FullName}");
        
        using var _ = context.StackAccess(appFieldType.App, target);
        return await context.SaveFieldDataAsync(appFieldType, appFieldType.ValueType!.From(values));
    }

    /// <summary>
    /// Save entity data
    /// </summary>
    public static Task<bool> SaveFieldEntityAsync<T>(this SchemaContext context, AppFieldType field, string target, T value)
    {
        context.AssertType<T>(field);
        if (field.Application.ScopeType != AppScopeType.SystemLevel && string.IsNullOrWhiteSpace(target))
            throw new ArgumentException($"The target is required for app field of type {typeof(T).FullName}");
        
        using var _ = context.StackAccess(field.App, target);
        return context.SaveFieldDataAsync(field, field.ValueType!.From(value));
    }

    /// <summary>
    /// Save entity list data
    /// </summary>
    public static Task<bool> SaveFieldEntitiesAsync<T>(this SchemaContext context, AppFieldType field, string target, List<T> values)
    {
        context.AssertType<T>(field);
        if (field.Application.ScopeType != AppScopeType.SystemLevel && string.IsNullOrWhiteSpace(target))
            throw new ArgumentException($"The target is required for app field of type {typeof(T).FullName}");
        
        using var _ = context.StackAccess(field.App, target);
        return context.SaveFieldDataAsync(field, field.ValueType!.From(values));
    }

    #endregion
}