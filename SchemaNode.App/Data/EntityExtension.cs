using System.Linq.Expressions;
using System.Reflection;
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
    /// Gets the entity data by condition
    /// </summary>
    public static async Task<T?> GetEntityAsync<T>(this SchemaContext context, string target, Expression<Func<T, bool>> cond, bool forUpdate = false, CancellationToken token = default)
    {
        AppFieldType appFieldType = await context.AssertAppField<T>().ConfigureAwait(false);
        if (appFieldType.Application.ScopeType != AppScopeType.SystemLevel && string.IsNullOrEmpty(target))
            return default;
        
        // first only, no more check
        AppSchemaDataFilter filter = AppSchemaDataFilterVisitor.Build(cond);
       
        using var stack = context.StackAccess(appFieldType.App, target);
        var (value, _) = await context.GetAppFieldDataAsync(appFieldType, AppSchemaDataResult.First, filter, forUpdate: forUpdate, genDisplayOnly: !forUpdate, token: token).ConfigureAwait(false);
        return value != null ? value.GetValue<T>() : default;
    }
    
    /// <summary>
    /// Gets the entity data by condition
    /// </summary>
    public static Task<T?> GetEntityAsync<T>(this SchemaContext context, Expression<Func<T, bool>> cond, bool forUpdate = false, CancellationToken token = default) 
        => context.GetEntityAsync(string.Empty, cond, forUpdate, token);
    
    /// <summary>
    /// Gets the entity list data by condition
    /// </summary>
    public static async Task<List<T>> GetEntitiesAsync<T>(this SchemaContext context, string target, Expression<Func<T, bool>> cond, int skip = 0, int take = 0, bool desc = false, AppSchemaDataOrder[]? orderBy = null, bool forUpdate = false, CancellationToken token = default)
    {
        AppFieldType appFieldType = await context.AssertAppField<T>().ConfigureAwait(false);
        if (appFieldType.Application.ScopeType != AppScopeType.SystemLevel && string.IsNullOrEmpty(target))
            return [];
        
        AppSchemaDataFilter filter = AppSchemaDataFilterVisitor.Build(cond);
        using var stack = context.StackAccess(appFieldType.App, target);
        
        var (value, _) = await context.GetAppFieldDataAsync(appFieldType, AppSchemaDataResult.List, filter,
            skip, take, desc, orderBy, forUpdate: forUpdate, genDisplayOnly: !forUpdate, token: token).ConfigureAwait(false);
        return value is ArrayNode arr ? arr.Select(o => o.GetValue<T>()!).ToList() : [];
    }
    
    /// <summary>
    /// Gets the entity list data by condition
    /// </summary>
    public static Task<List<T>> GetEntitiesAsync<T>(this SchemaContext context, Expression<Func<T, bool>> cond,  int skip = 0, int take = 0, bool desc = false, AppSchemaDataOrder[]? orderBy = null, bool forUpdate = false, CancellationToken token = default)
        => context.GetEntitiesAsync(string.Empty, cond, skip, take, desc, orderBy, forUpdate, token);

    /// <summary>
    /// Gets the entity list data with total by condition
    /// </summary>
    public static async Task<(List<T>, int)> GetEntitiesWithTotalAsync<T>(this SchemaContext context, string target, Expression<Func<T, bool>> cond, int skip = 0, int take = 0, bool desc = false, AppSchemaDataOrder[]? orderBy = null, bool forUpdate = false, CancellationToken token = default)
    {
        AppFieldType appFieldType = await context.AssertAppField<T>().ConfigureAwait(false);
        if (appFieldType.Application.ScopeType != AppScopeType.SystemLevel && string.IsNullOrEmpty(target))
            return ([], 0);
        
        AppSchemaDataFilter filter = AppSchemaDataFilterVisitor.Build(cond);
        using var stack = context.StackAccess(appFieldType.App, target);
        
        var (value, total) = await context.GetAppFieldDataAsync(appFieldType, AppSchemaDataResult.List, filter,
            skip, take, desc, orderBy, forUpdate: forUpdate, genDisplayOnly: !forUpdate, token: token).ConfigureAwait(false);
        return (value is ArrayNode arr ? arr.Select(o => o.GetValue<T>()!).ToList() : [], total);
    }
    
    /// <summary>
    /// Gets the entity list data with total by condition
    /// </summary>
    public static Task<(List<T>, int)> GetEntitiesWithTotalAsync<T>(this SchemaContext context, Expression<Func<T, bool>> cond,  int skip = 0, int take = 0, bool desc = false, AppSchemaDataOrder[]? orderBy = null, bool forUpdate = false, CancellationToken token = default)
        => context.GetEntitiesWithTotalAsync(string.Empty, cond, skip, take, desc, orderBy, forUpdate, token);

    #endregion

    #region Delete

    /// <summary>
    /// Delete entity data
    /// </summary>
    public static async Task<bool> DeleteEntityAsync<T>(this SchemaContext context, string target, T value, CancellationToken token = default)
    {
        AppFieldType appFieldType = await context.AssertAppField<T>().ConfigureAwait(false);
        if (appFieldType.Application.ScopeType != AppScopeType.SystemLevel && string.IsNullOrWhiteSpace(target))
            throw new ArgumentException($"The target is required for app field of type {typeof(T).FullName}");
        
        using var _ = context.StackAccess(appFieldType.App, target);
        if (!appFieldType.GetPrimaryProperties().Any())  return await context.SaveFieldDataAsync(appFieldType, null, token: token).ConfigureAwait(false);

        var node = (appFieldType.ValueType as ArrayType)?.Element?.Create(context, value) ?? throw new ArgumentException($"The value of type {typeof(T).FullName} is invalid for delete");
        return await context.DeleteFieldListDataAsync(appFieldType, node, token).ConfigureAwait(false);
    }
    /// <summary>
    /// Delete entity data
    /// </summary>
    public static Task<bool> DeleteEntityAsync<T>(this SchemaContext context, T value, CancellationToken token = default) => context.DeleteEntityAsync(string.Empty, value, token);

    /// <summary>
    /// Delete entity data
    /// </summary>
    public static async Task<bool> DeleteEntityAsync<T>(this SchemaContext context, string? target, CancellationToken token = default, params object[] keys)
    {
        AppFieldType appFieldType = await context.AssertAppField<T>().ConfigureAwait(false);
        if (appFieldType.Application.ScopeType != AppScopeType.SystemLevel && string.IsNullOrWhiteSpace(target))
            throw new ArgumentException($"The target is required for app field of type {typeof(T).FullName}");
        
        using var _ = context.StackAccess(appFieldType.App, target);
        IReadOnlyList<PropertyInfo> primaries = appFieldType.GetPrimaryProperties();
        if (primaries.Count == 0)
            return await context.SaveFieldDataAsync(appFieldType, null, token: token).ConfigureAwait(false);

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
        return await context.DeleteFieldListDataAsync(appFieldType, filter, token).ConfigureAwait(false);
    }
    
    /// <summary>
    /// Delete entity data
    /// </summary>
    public static async Task<bool> DeleteEntitiesAsync<T>(this SchemaContext context, string target, List<T> value, CancellationToken token = default)
    {
        AppFieldType appFieldType = await context.AssertAppField<T>().ConfigureAwait(false);
        var primaries = appFieldType.GetPrimaryProperties();
        if (primaries == null) throw new ArgumentException($"The app field of type {typeof(T).FullName} only support single value");
        if (appFieldType.Application.ScopeType != AppScopeType.SystemLevel && string.IsNullOrWhiteSpace(target))
            throw new ArgumentException($"The target is required for app field of type {typeof(T).FullName}");

        using var _ = context.StackAccess(appFieldType.App, target);
        
        var array = new ArrayNode(context, appFieldType.ValueType!);
        foreach (T valueItem in value)
        {
            var node = (appFieldType.ValueType as ArrayType)?.Element?.Create(context, valueItem) ?? throw new ArgumentException($"The value of type {typeof(T).FullName} is invalid for delete");
            array.Add(node);
        }

        return await context.DeleteFieldListDataAsync(appFieldType, array, token).ConfigureAwait(false);
    }
    
    /// <summary>
    /// Delete entity data
    /// </summary>
    public static Task<bool> DeleteEntitiesAsync<T>(this SchemaContext context, List<T> value, CancellationToken token = default) => context.DeleteEntitiesAsync(string.Empty, value, token);

    /// <summary>
    /// Delete entity data
    /// </summary>
    public static async Task<bool> DeleteEntitiesAsync<T>(this SchemaContext context, string target, Expression<Func<T, bool>> cond, CancellationToken token = default)
        => await context.DeleteFieldEntityAsync(await context.AssertAppField<T>().ConfigureAwait(false), target, cond, token).ConfigureAwait(false);

    /// <summary>
    /// Delete entity data
    /// </summary>
    public static async Task<bool> DeleteEntitiesAsync<T>(this SchemaContext context, Expression<Func<T, bool>> cond, CancellationToken token = default)
        => await context.DeleteFieldEntityAsync(await context.AssertAppField<T>().ConfigureAwait(false), string.Empty, cond, token).ConfigureAwait(false);

    /// <summary>
    /// Delete entity data
    /// </summary>
    public static async Task<bool> DeleteFieldEntityAsync<T>(this SchemaContext context, AppFieldType field, string target, T value, CancellationToken token = default)
    {
        context.AssertType<T>(field);
        if (field.Application.ScopeType != AppScopeType.SystemLevel && string.IsNullOrWhiteSpace(target))
            throw new ArgumentException($"The target is required for app field of type {typeof(T).FullName}");
        using var _ = context.StackAccess(field.App, target);
        var node = (field.ValueType as ArrayType)?.Element?.Create(context, value) ?? throw new ArgumentException($"The value of type {typeof(T).FullName} is invalid for delete");
        return await context.DeleteFieldListDataAsync(field, node, token).ConfigureAwait(false);
    }
    
    /// <summary>
    /// Delete entity data
    /// </summary>
    public static Task<bool> DeleteFieldEntityAsync<T>(this SchemaContext context, AppFieldType field, T value, CancellationToken token = default)
        => context.DeleteFieldEntityAsync(field, string.Empty, value, token);

    /// <summary>
    /// Delete entity data
    /// </summary>
    public static async Task<bool> DeleteFieldEntityAsync<T>(this SchemaContext context, AppFieldType field, string target, List<T> value, CancellationToken token = default)
    {
        context.AssertType<T>(field);
        if (field.Application.ScopeType != AppScopeType.SystemLevel && string.IsNullOrWhiteSpace(target))
            throw new ArgumentException($"The target is required for app field of type {typeof(T).FullName}");
        
        using var _ = context.StackAccess(field.App, target);
        var array = new ArrayNode(context, field.ValueType!);
        foreach (T valueItem in value)
        {
            var node = (field.ValueType as ArrayType)?.Element?.Create(context, valueItem) ?? throw new ArgumentException($"The value of type {typeof(T).FullName} is invalid for delete");
            array.Add(node);
        }
        return await context.DeleteFieldListDataAsync(field, array, token).ConfigureAwait(false);
    }
    /// <summary>
    /// Delete entity data
    /// </summary>
    public static Task<bool> DeleteFieldEntityAsync<T>(this SchemaContext context, AppFieldType field, List<T> value, CancellationToken token = default)
        => context.DeleteFieldEntityAsync(field, string.Empty, value, token);

    /// <summary>
    /// Delete entity data
    /// </summary>
    public static Task<bool> DeleteFieldEntityAsync<T>(this SchemaContext context, AppFieldType field, string target, Expression<Func<T, bool>> cond, CancellationToken token = default)
    {
        context.AssertType<T>(field);
        if (field.Application.ScopeType != AppScopeType.SystemLevel && string.IsNullOrWhiteSpace(target))
            throw new ArgumentException($"The target is required for app field of type {typeof(T).FullName}");
        
        using var _ = context.StackAccess(field.App, target);
        AppSchemaDataFilter filter = AppSchemaDataFilterVisitor.Build(cond);
        return context.DeleteFieldListDataAsync(field, filter, token);
    }
    
    /// <summary>
    /// Delete entity data
    /// </summary>
    public static Task<bool> DeleteFieldEntityAsync<T>(this SchemaContext context, AppFieldType field, Expression<Func<T, bool>> cond, CancellationToken token = default)
        => context.DeleteFieldEntityAsync(field, string.Empty, cond, token);

    #endregion
    
    #region Update
    
    /// <summary>
    /// Save entity data
    /// </summary>
    public static async Task<bool> SaveEntityAsync<T>(this SchemaContext context, string target, T value, CancellationToken token = default)
    {
        AppFieldType appFieldType = await context.AssertAppField<T>().ConfigureAwait(false);
        if (appFieldType.Application.ScopeType != AppScopeType.SystemLevel && string.IsNullOrWhiteSpace(target))
            throw new ArgumentException($"The target is required for app field of type {typeof(T).FullName}");
        
        using var stack = context.StackAccess(appFieldType.App, target);
        return await context.SaveFieldDataAsync(appFieldType, appFieldType.ValueType!.Create(context, value), token: token).ConfigureAwait(false);
    }

    /// <summary>
    /// Save entity data
    /// </summary>
    public static Task<bool> SaveEntityAsync<T>(this SchemaContext context, T value, CancellationToken token = default) => context.SaveEntityAsync(string.Empty, value, token);

    /// <summary>
    /// Save entity list data
    /// </summary>
    public static async Task<bool> SaveEntitiesAsync<T>(this SchemaContext context, string target, List<T> values, CancellationToken token = default)
    {
        AppFieldType appFieldType = await context.AssertAppField<T>().ConfigureAwait(false);
        var primaries = appFieldType.GetPrimaryProperties();
        if (primaries == null) throw new ArgumentException($"The app field of {typeof(T).FullName} only support single value");
        if (appFieldType.Application.ScopeType != AppScopeType.SystemLevel && string.IsNullOrWhiteSpace(target))
            throw new ArgumentException($"The target is required for app field of type {typeof(T).FullName}");
        
        using var _ = context.StackAccess(appFieldType.App, target);
        return await context.SaveFieldDataAsync(appFieldType, appFieldType.ValueType!.Create(context, values), token: token).ConfigureAwait(false);
    }
    
    /// <summary>
    /// Save entity list data
    /// </summary>
    public static Task<bool> SaveEntitiesAsync<T>(this SchemaContext context, List<T> values, CancellationToken token = default) => context.SaveEntitiesAsync(string.Empty, values, token);

    /// <summary>
    /// Save entity data
    /// </summary>
    public static Task<bool> SaveFieldEntityAsync<T>(this SchemaContext context, AppFieldType field, string target, T value, CancellationToken token = default)
    {
        context.AssertType<T>(field);
        if (field.Application.ScopeType != AppScopeType.SystemLevel && string.IsNullOrWhiteSpace(target))
            throw new ArgumentException($"The target is required for app field of type {typeof(T).FullName}");
        
        using var _ = context.StackAccess(field.App, target);
        return context.SaveFieldDataAsync(field, field.ValueType!.Create(context, value), token: token);
    }
    
    /// <summary>
    /// Save entity data
    /// </summary>
    public static Task<bool> SaveFieldEntityAsync<T>(this SchemaContext context, AppFieldType field, T value, CancellationToken token = default) => context.SaveFieldEntityAsync(field, string.Empty, value, token);

    /// <summary>
    /// Save entity list data
    /// </summary>
    public static Task<bool> SaveFieldEntitiesAsync<T>(this SchemaContext context, AppFieldType field, string target, List<T> values, CancellationToken token = default)
    {
        context.AssertType<T>(field);
        if (field.Application.ScopeType != AppScopeType.SystemLevel && string.IsNullOrWhiteSpace(target))
            throw new ArgumentException($"The target is required for app field of type {typeof(T).FullName}");
        
        using var _ = context.StackAccess(field.App, target);
        return context.SaveFieldDataAsync(field, field.ValueType!.Create(context, values), token: token);
    }
    
    /// <summary>
    /// Save entity list data
    /// </summary>
    public static Task<bool> SaveFieldEntitiesAsync<T>(this SchemaContext context, AppFieldType field, List<T> values, CancellationToken token = default) => context.SaveFieldEntitiesAsync(field, string.Empty, values, token);

    #endregion
}