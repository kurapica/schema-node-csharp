using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json.Nodes;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Utility;

namespace SchemaNode.Components;

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
        (AppFieldType appFieldType, IReadOnlyList<PropertyInfo>? primaries) = await context.AssertAppField<T>();
        if (appFieldType.Application.ScopeType != AppScopeType.SystemLevel && string.IsNullOrEmpty(target))
            return default;
        
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
        
        using var _ = context.StackAccess(appFieldType.App, target);
        var (value, _) = await context.GetAppFieldDataAsync(appFieldType, AppSchemaDataResult.First, filter);
        return value != null ? value.ToValue<T>() : default;
    }

    /// <summary>
    /// Gets the entity data by full primary keys
    /// </summary>
    public static async Task<T?> GetEntityAsync<T>(this SchemaContext context, string? target, Expression<Func<T, bool>> cond, bool forUpdate = false)
    {
        (AppFieldType appFieldType, _) = await context.AssertAppField<T>();
        if (appFieldType.Application.ScopeType != AppScopeType.SystemLevel && string.IsNullOrEmpty(target))
            return default;
        
        // first only, no more check
        AppSchemaDataFilter filter = AppSchemaDataFilterVisitor.Build(cond);
       
        using var stack = context.StackAccess(appFieldType.App, target);
        var (value, _) = await context.GetAppFieldDataAsync(appFieldType, AppSchemaDataResult.First, filter,
            forUpdate: forUpdate);
        return value != null ? value.ToValue<T>() : default;
    }

    /// <summary>
    /// Gets the entity data by full primary keys
    /// </summary>
    public static async Task<List<T>> GetEntitiesAsync<T>(this SchemaContext context, string? target, Expression<Func<T, bool>> cond, bool forUpdate = false)
    {
        (AppFieldType appFieldType, _) = await context.AssertAppField<T>();
        if (appFieldType.Application.ScopeType != AppScopeType.SystemLevel && string.IsNullOrEmpty(target))
            return [];
        
        AppSchemaDataFilter filter = AppSchemaDataFilterVisitor.Build(cond);
        
        using var stack = context.StackAccess(appFieldType.App, target);
        var (value, _) = await context.GetAppFieldDataAsync(appFieldType, AppSchemaDataResult.List, 
            filter, forUpdate: forUpdate);
        return value is ArrayTypeNode arr ? arr.Select(o => o.ToValue<T>()!).ToList() : [];
    }

    /// <summary>
    /// Gets the entity data by full primary keys
    /// </summary>
    public static async Task<(List<T> value, int total)> GetEntitiesAsync<T>(this SchemaContext context, string? target, Expression<Func<T, bool>> cond, int take, int skip = 0, bool desc = false, AppSchemaDataOrder[]? orderBy = null, bool forUpdate = false)
    {
        (AppFieldType appFieldType, _) = await context.AssertAppField<T>();
        if (appFieldType.Application.ScopeType != AppScopeType.SystemLevel && string.IsNullOrEmpty(target))
            return ([], 0);
        
        AppSchemaDataFilter filter = AppSchemaDataFilterVisitor.Build(cond);
        using var stack = context.StackAccess(appFieldType.App, target);
        
        var (value, total) = await context.GetAppFieldDataAsync(appFieldType, AppSchemaDataResult.List, filter,
            skip, take, desc, orderBy, forUpdate: forUpdate);
        return (value is ArrayTypeNode arr ? arr.Select(o => o.ToValue<T>()!).ToList() : [], total);
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
        return (value is ArrayTypeNode arr ? arr.Select(o => o.ToValue<T>()!).ToList() : [], total);
    }

    #endregion

    #region Delete

    /// <summary>
    /// Delete entity data
    /// </summary>
    public static async Task<bool> DeleteEntityAsync<T>(this SchemaContext context, string? target, T value)
    {
        (AppFieldType appFieldType, IReadOnlyList<PropertyInfo>? primaries) = await context.AssertAppField<T>();
        if (appFieldType.Application.ScopeType != AppScopeType.SystemLevel && string.IsNullOrWhiteSpace(target))
            throw new ArgumentException($"The target is required for app field of type {typeof(T).FullName}");
        
        using var _ = context.StackAccess(appFieldType.App, target);
        if (primaries == null)  return await context.SaveFieldDataAsync(appFieldType, null);

        var node = await context.GetSchemaNodeAsync(value) ?? throw new ArgumentException($"The value of type {typeof(T).FullName} is invalid for delete");
        return await context.DeleteFieldListDataAsync(appFieldType, node);
    }

    /// <summary>
    /// Delete entity data
    /// </summary>
    public static async Task<bool> DeleteEntityAsync<T>(this SchemaContext context, string? target, params object[] keys)
    {
        (AppFieldType appFieldType, IReadOnlyList<PropertyInfo>? primaries) = await context.AssertAppField<T>();
        if (appFieldType.Application.ScopeType != AppScopeType.SystemLevel && string.IsNullOrWhiteSpace(target))
            throw new ArgumentException($"The target is required for app field of type {typeof(T).FullName}");
        
        using var _ = context.StackAccess(appFieldType.App, target);

        if (primaries == null || primaries.Count == 0)
            return await context.SaveFieldDataAsync(appFieldType, null);

        if (keys.Length != primaries.Count)
            throw new ArgumentException($"The type {typeof(T).FullName} primary key count not match");
        
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
        (AppFieldType appFieldType, IReadOnlyList<PropertyInfo>? primaries) = await context.AssertAppField<T>();
        if (primaries == null) throw new ArgumentException($"The app field of type {typeof(T).FullName} only support single value");
        if (appFieldType.Application.ScopeType != AppScopeType.SystemLevel && string.IsNullOrWhiteSpace(target))
            throw new ArgumentException($"The target is required for app field of type {typeof(T).FullName}");

        using var _ = context.StackAccess(appFieldType.App, target);
        
        ArrayTypeNode array = new ArrayTypeNode(appFieldType.SchemaType!);
        foreach (T valueItem in value)
        {
            var node = await context.GetSchemaNodeAsync(valueItem) ??
                       throw new ArgumentException($"The value of type {typeof(T).FullName} is invalid for delete");
            array.Add(node);
        }

        return await context.DeleteFieldListDataAsync(appFieldType, array);
    }

    /// <summary>
    /// Delete entity data
    /// </summary>
    public static async Task<bool> DeleteEntitiesAsync<T>(this SchemaContext context, string? target, Expression<Func<T, bool>> cond)
    {
        (AppFieldType appFieldType, _) = await context.AssertAppField<T>();
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
        var node = await context.GetSchemaNodeAsync(value) ?? throw new ArgumentException($"The value of type {typeof(T).FullName} is invalid for delete");
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
        ArrayTypeNode array = new ArrayTypeNode(field.SchemaType!);
        foreach (T valueItem in value)
        {
            var node = await context.GetSchemaNodeAsync(valueItem) ?? throw new ArgumentException($"The value of type {typeof(T).FullName} is invalid for delete");
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
        (AppFieldType appFieldType, _) = await context.AssertAppField<T>();
        if (appFieldType.Application.ScopeType != AppScopeType.SystemLevel && string.IsNullOrWhiteSpace(target))
            throw new ArgumentException($"The target is required for app field of type {typeof(T).FullName}");
        
        using var stack = context.StackAccess(appFieldType.App, target);
        return await context.SaveFieldDataAsync(appFieldType, appFieldType.SchemaType!.CreateNode(value));
    }

    /// <summary>
    /// Save entity list data
    /// </summary>
    public static async Task<bool> SaveEntitiesAsync<T>(this SchemaContext context, string? target, List<T> values)
    {
        (AppFieldType appFieldType, IReadOnlyList<PropertyInfo>? primaries) = await context.AssertAppField<T>();
        if (primaries == null) throw new ArgumentException($"The app field of {typeof(T).FullName} only support single value");
        if (appFieldType.Application.ScopeType != AppScopeType.SystemLevel && string.IsNullOrWhiteSpace(target))
            throw new ArgumentException($"The target is required for app field of type {typeof(T).FullName}");
        
        using var _ = context.StackAccess(appFieldType.App, target);
        return await context.SaveFieldDataAsync(appFieldType, appFieldType.SchemaType!.CreateNode(values));
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
        return context.SaveFieldDataAsync(field, field.SchemaType!.CreateNode(value));
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
        return context.SaveFieldDataAsync(field, field.SchemaType!.CreateNode(values));
    }

    #endregion
}