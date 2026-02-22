using SchemaNode.Attribute;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Utility;
using System.Text.Json.Nodes;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global

namespace SchemaNode.Function;

/// <summary>
/// The system.data api
/// </summary>
[Schema(NS_SYSTEM_DATA)]
public static class SystemData
{
    #region Context Item

    /// <summary>
    /// Gets the context item
    /// </summary>
    [Schema]
    public static AnySchemaNode? getcontextitem(SchemaContext context, string item) 
        => context.GetSchemaContextItem(item);

    #endregion

    #region Get single Application Data

    /// <summary>
    /// Gets the application data if single value
    /// </summary>
    [Schema]
    public static async Task<T?> getappdata<T>(
        SchemaContext context,
        [Schema(NS_SYSTEM_SCHEMA_APP)] string app,
        string field,
        [Schema(NS_SYSTEM_SCHEMA_APP_TARGET)] string? target)
    {
        if (string.IsNullOrEmpty(target))
        {
            target = context.GetSchemaContextItem<Access>()?.Target;
            if (string.IsNullOrEmpty(target)) return default;
        }

        AppType? appType = !string.IsNullOrEmpty(app) ? await context.GetAppTypeAsync(app) : null;
        AppFieldType? fieldType = appType?.GetField(field);
        if (fieldType is not { EnableDynamicTable: true } || !fieldType.Single) return default;

        using var stack = context.StackAccess(appType!.Name, target);
        var (value, _) = await context.GetAppFieldDataAsync(fieldType, AppSchemaDataResult.List);
        return value != null ? value.ToValue<T>() : default;
    }
    
    static async Task<AnySchemaNode?> getappdatainner(
        SchemaContext context,
        string app,
        string field,
        string target,
        params object?[] args)
    {
        // get the app field type
        AppType? appType = !string.IsNullOrEmpty(app) ? await context.GetAppTypeAsync(app) : null;
        AppFieldType? fieldType = appType?.GetField(field);
        if (fieldType is not { EnableDynamicTable: true } || 
            fieldType.SchemaType is not ArrayType { Primary: { Length: > 0 }} arrType ||
            arrType.Primary.Length != args.Length) return null;
        
        using AccessScope stack = context.StackAccess(app, target);

        // get the key type
        string[] keys = arrType.Primary;
        List<AnySchemaNode>[] keyValues = new List<AnySchemaNode>[keys.Length];
        AppSchemaDataFilter? filter = null;
        for (int i = 0; i < keys.Length; i++)
        {
            AnySchemaType? keyType = (arrType.ElementSchemaType as StructType)?.GetField(keys[i])?.SchemeType;
            AnySchemaNode? valueNode = keyType?.CreateNode(args[i]);
            if (valueNode == null || valueNode.IsEmpty) return null;

            if (keyType is EnumType { Cascade: { Length: > 1 } } enumType &&
                fieldType.Filters != null &&
                fieldType.Filters.Any(f =>
                    f.Filter.Equals(keys[i], StringComparison.OrdinalIgnoreCase) &&
                    f.Resolve == Enum.FieldFilterResolve.CascadeParent))
            {
                EnumValueAccess[] access = await enumType.LoadEnumAccessListAsync(context, valueNode.ToString(),
                    noSubList: true, withSubList: false);
                if (access.Length == 0) return null;
                keyValues[i] = access.Select(a => keyType.CreateNode(a.Value)!).ToList();
            }
            else
            {
                keyValues[i] = [valueNode];
            }

            // filter
            var keyFilter = keyValues[i].Count > 1
                ? new AppSchemaDataFilterBinary(LogicType.Contains,
                    new AppSchemaDataFilterValue(keyValues[i]),
                    new AppSchemaDataFilterField(keys[i]))
                : new AppSchemaDataFilterBinary(LogicType.Equal,
                    new AppSchemaDataFilterField(keys[i]),
                    new AppSchemaDataFilterValue(keyValues[i][0]));

            filter = filter == null
                ? keyFilter
                : new AppSchemaDataFilterBinary(LogicType.AndAlso, filter, keyFilter);
        }

        (AnySchemaNode? value, _) = await context.GetAppFieldDataAsync(fieldType, AppSchemaDataResult.List, filter);
        if (value is not ArrayTypeNode { Count: > 0 } result) return null;

        // find the match item
        StructTypeNode[] items = result.Cast<StructTypeNode>().ToArray();
        for (int i = 0; i < keyValues.Length; i++)
        {
            if (keyValues[i].Count == 1) continue;

            // match the last
            for (int j = keyValues[i].Count - 1; j >= 0; j--)
            {
                AnySchemaNode key = keyValues[i][j];
                if (!items.Any(n => key.Equals(n.GetField(keys[i])!))) continue;
                items = items.Where(n => key.Equals(n.GetField(keys[i])!)).ToArray();
                break;
            }
        }

        return items.Length > 0 ? items[0] : null;
    }
    
    /// <summary>
    /// Gets the application data by one key
    /// </summary>
    [Schema]
    public static async Task<T?> getappdatabyonekey<T, T1>(
        SchemaContext context,
        [Schema(NS_SYSTEM_SCHEMA_APP)] string app,
        string field,
        T1 key,
        [Schema(NS_SYSTEM_SCHEMA_APP_TARGET)] string? target)
    {
        if (string.IsNullOrEmpty(target))
        {
            target = context.GetSchemaContextItem<Access>()?.Target;
            if (string.IsNullOrEmpty(target)) return default;
        }

        AnySchemaNode? value = await getappdatainner(context, app, field, target, key);
        return value switch
        {
            ArrayTypeNode { Count: > 0 } arrayNode => arrayNode.First().ToValue<T>(),
            { IsEmpty: false } => value.ToValue<T>(),
            _ => default
        };
    }

    /// <summary>
    /// Gets the application data by one key
    /// </summary>
    [Schema]
    public static async Task<T?> getappdatabytwokey<T, T1, T2>(
        SchemaContext context,
        [Schema(NS_SYSTEM_SCHEMA_APP)] string app,
        string field,
        T1 key1, T2 key2,
        [Schema(NS_SYSTEM_SCHEMA_APP_TARGET)] string? target)
    {
        if (string.IsNullOrEmpty(target))
        {
            target = context.GetSchemaContextItem<Access>()?.Target;
            if (string.IsNullOrEmpty(target)) return default;
        }

        AnySchemaNode? value = await getappdatainner(context, app, field, target, key1, key2);
        return value switch
        {
            ArrayTypeNode { Count: > 0 } arrayNode => arrayNode.First().ToValue<T>(),
            { IsEmpty: false } => value.ToValue<T>(),
            _ => default
        };
    }

    /// <summary>
    /// Gets the application data by one key
    /// </summary>
    [Schema]
    public static async Task<T?> getappdatabythreekey<T, T1, T2, T3>(
        SchemaContext context,
        [Schema(NS_SYSTEM_SCHEMA_APP)] string app,
        string field,
        T1 key1, T2 key2, T3 key3,
        [Schema(NS_SYSTEM_SCHEMA_APP_TARGET)] string? target)
    {
        if (string.IsNullOrEmpty(target))
        {
            target = context.GetSchemaContextItem<Access>()?.Target;
            if (string.IsNullOrEmpty(target)) return default;
        }

        AnySchemaNode? value = await getappdatainner(context, app, field, target, key1, key2, key3);
        return value switch
        {
            ArrayTypeNode { Count: > 0 } arrayNode => arrayNode.First().ToValue<T>(),
            { IsEmpty: false } => value.ToValue<T>(),
            _ => default
        };
    }

    /// <summary>
    /// Gets the application data by one key
    /// </summary>
    [Schema]
    public static async Task<T?> getappdatabyfourkey<T, T1, T2, T3, T4>(
        SchemaContext context,
        [Schema(NS_SYSTEM_SCHEMA_APP)] string app,
        string field,
        T1 key1, T2 key2, T3 key3, T4 key4,
        [Schema(NS_SYSTEM_SCHEMA_APP_TARGET)] string? target)
    {
        if (string.IsNullOrEmpty(target))
        {
            target = context.GetSchemaContextItem<Access>()?.Target;
            if (string.IsNullOrEmpty(target)) return default;
        }

        AnySchemaNode? value = await getappdatainner(context, app, field, target, key1, key2, key3, key4);
        return value switch
        {
            ArrayTypeNode { Count: > 0 } arrayNode => arrayNode.First().ToValue<T>(),
            { IsEmpty: false } => value.ToValue<T>(),
            _ => default
        };
    }

    #endregion

    #region Get Application Data For field
        
    /// <summary>
    /// Gets the application data for field if single value
    /// </summary>
    [Schema]
    public static async Task<T?> getappfdata<T>(
        SchemaContext context,
        [Schema(NS_SYSTEM_SCHEMA_APP)] string app,
        string field,
        string dataField,
        [Schema(NS_SYSTEM_SCHEMA_APP_TARGET)] string? target)
    {
        AnySchemaNode? result = await getappdata<AnySchemaNode>(context, app, field, target);
        AnySchemaNode? f = (result as StructTypeNode)?.GetField(dataField);
        return f != null ? f.ToValue<T>() : default;
    }
    
    /// <summary>
    /// Gets the application data by one key
    /// </summary>
    [Schema]
    public static async Task<T?> getappfdatabyonekey<T, T1>(
        SchemaContext context,
        [Schema(NS_SYSTEM_SCHEMA_APP)] string app,
        string field,
        string dataField,
        T1 key,
        [Schema(NS_SYSTEM_SCHEMA_APP_TARGET)] string? target)
    {
        AnySchemaNode? result = await getappdatabyonekey<AnySchemaNode, T1>(context, app, field, key, target);
        AnySchemaNode? f = (result as StructTypeNode)?.GetField(dataField);
        return f != null ? f.ToValue<T>() : default;
    }

    /// <summary>
    /// Gets the application data by one key
    /// </summary>
    [Schema]
    public static async Task<T?> getappfdatabytwokey<T, T1, T2>(
        SchemaContext context,
        [Schema(NS_SYSTEM_SCHEMA_APP)] string app,
        string field,
        string dataField,
        T1 key1, T2 key2,
        [Schema(NS_SYSTEM_SCHEMA_APP_TARGET)] string? target)
    {
        AnySchemaNode? result = await getappdatabytwokey<AnySchemaNode, T1, T2>(context, app, field, key1, key2, target);
        AnySchemaNode? f = (result as StructTypeNode)?.GetField(dataField);
        return f != null ? f.ToValue<T>() : default;
    }

    /// <summary>
    /// Gets the application data by one key
    /// </summary>
    [Schema]
    public static async Task<T?> getappfdatabythreekey<T, T1, T2, T3>(
        SchemaContext context,
        [Schema(NS_SYSTEM_SCHEMA_APP)] string app,
        string field,
        string dataField,
        T1 key1, T2 key2, T3 key3,
        [Schema(NS_SYSTEM_SCHEMA_APP_TARGET)] string? target)
    {
        AnySchemaNode? result = await getappdatabythreekey<AnySchemaNode, T1, T2, T3>(context, app, field, key1, key2, key3, target);
        AnySchemaNode? f = (result as StructTypeNode)?.GetField(dataField);
        return f != null ? f.ToValue<T>() : default;
    }

    /// <summary>
    /// Gets the application data by one key
    /// </summary>
    [Schema]
    public static async Task<T?> getappfdatabyfourkey<T, T1, T2, T3, T4>(
        SchemaContext context,
        [Schema(NS_SYSTEM_SCHEMA_APP)] string app,
        string field,
        string dataField,
        T1 key1, T2 key2, T3 key3, T4 key4,
        [Schema(NS_SYSTEM_SCHEMA_APP_TARGET)] string? target)
    {
        AnySchemaNode? result = await getappdatabyfourkey<AnySchemaNode, T1, T2, T3, T4>(context, app, field, key1, key2, key3, key4, target);
        AnySchemaNode? f = (result as StructTypeNode)?.GetField(dataField);
        return f != null ? f.ToValue<T>() : default;
    }

    #endregion

    #region Data Source

    /// <summary>
    /// Generate a data source for the app field, waiting for query
    /// </summary>
    [Schema]
    public static async Task<ArrayTypeNode> getdatasource(
        SchemaContext context,
        [Schema(NS_SYSTEM_SCHEMA_APP)] string app,
        string field,
        [Schema(NS_SYSTEM_SCHEMA_APP_TARGET)] string? target)
    {
        AppType? appType = !string.IsNullOrEmpty(app)
            ? await context.GetAppTypeAsync(app)
            : null;

        AppFieldType? fieldType = appType?.GetField(field);
        if (fieldType?.SchemaType == null) throw new InvalidOperationException($"The field {field} not found in the app {app}.");
        if (fieldType.SchemaType is not ArrayType) throw new InvalidOperationException($"The field {field} type is not array type in the app {app}.");
        return new ArrayTypeNode(fieldType.SchemaType!);
    }

    #endregion
    
    #region Write App Data

    /// <summary>
    /// Incr the app field data
    /// </summary>
    [Schema]
    [SideEffect]
    public static async Task<JsonNode?> incrappdata(
        SchemaContext context,
        [Schema(NS_SYSTEM_SCHEMA_APP)] string app,
        [Schema(NS_SYSTEM_SCHEMA_APP_FIELD)] string field,
        [Schema(NS_GENERIC_TYPE)] JsonNode data,
        string target,
        bool raiseEvent = false
    )
    {
        if (string.IsNullOrEmpty(target)) return null;

        AppType? appType = !string.IsNullOrEmpty(app)
            ? await context.GetAppTypeAsync(app)
            : null;

        AppFieldType? fieldType = appType?.GetField(field);
        if (fieldType == null 
            || fieldType.SchemaType is EnumType 
            || fieldType.SchemaType is ScalarType { IsNumber: false } 
            || fieldType.SchemaType is StructType s && !s.Fields.Any(f => f.SchemeType is ScalarType {  IsNumber: true })
            || fieldType.SchemaType is ArrayType a && (a.ElementSchemaType is not StructType || a.Primary == null || a.Primary.Length == 0)
            ) return null;

        AnySchemaNode? dataNode = fieldType.SchemaType?.CreateNode(data);
        if (dataNode == null || dataNode.IsEmpty) return null;

        using var stack = context.StackAccess(appType!.Name, target);
        await context.BeginTransactionAsync();
        AnySchemaNode? origin = await context.GetAppFieldDataAsync(fieldType, dataNode, forUpdate: true);
        if (origin == null) goto ROLLBACK;

        switch (fieldType.SchemaType)
        {
            case ScalarType:
                {
                    if (dataNode is not ScalarTypeNode) goto ROLLBACK;
                    origin = fieldType.SchemaType.CreateNode(
                        (origin is { IsEmpty: false } ? origin.ToValue<decimal>() : 0m) +
                        (dataNode is { IsEmpty: false } ? dataNode.ToValue<decimal>() : 0m)
                    );
                    break;
                }
            case StructType @struct:
                {
                    if (dataNode is not StructTypeNode structData || origin is not StructTypeNode originStruct) goto ROLLBACK;
                    foreach (var fld in @struct.Fields)
                    {
                        AnySchemaNode? orgFld = originStruct.GetField(fld.Name);
                        AnySchemaNode? dataFld = structData.GetField(fld.Name);

                        if (orgFld?.SchemaType is ScalarType { IsNumber: true } && dataFld?.SchemaType is ScalarType { IsNumber: true })
                        {
                            decimal orgVal = orgFld is { IsEmpty: false } ? orgFld.ToValue<decimal>() : 0m;
                            decimal dataVal = dataFld is { IsEmpty: false } ? dataFld.ToValue<decimal>() : 0m;
                            originStruct.SetField(fld.Name, fld.SchemeType?.CreateNode(orgVal + dataVal));
                        }
                    }
                    break;
                }
            case ArrayType arrType:
                {
                    if (dataNode is not ArrayTypeNode arrayData || origin is not ArrayTypeNode originArray) goto ROLLBACK;
                    Dictionary<string, StructTypeNode> arrDict = new();
                    foreach(var i in arrayData)
                    {
                        var ditem = (StructTypeNode)i;
                        string? pkey = arrType.GetPrimaryKey(ditem);
                        if (pkey != null)
                        {
                            arrDict[pkey] = ditem;
                        }
                    }
                    StructType arrStruct = arrType.ElementSchemaType as StructType 
                        ?? throw new InvalidOperationException("Array element type is not struct type.");
                    foreach (var i in originArray)
                    {
                        var oitem = (StructTypeNode)i;
                        string? pkey = arrType.GetPrimaryKey(oitem);
                        if (pkey != null && arrDict.TryGetValue(pkey, out StructTypeNode? ditem))
                        {
                            foreach (var fld in arrStruct.Fields)
                            {
                                AnySchemaNode? orgFld = oitem.GetField(fld.Name);
                                AnySchemaNode? dataFld = ditem.GetField(fld.Name);

                                if (orgFld?.SchemaType is ScalarType { IsNumber: true } && dataFld?.SchemaType is ScalarType { IsNumber: true })
                                {
                                    decimal orgVal = orgFld is { IsEmpty: false } ? orgFld.ToValue<decimal>() : 0m;
                                    decimal dataVal = dataFld is { IsEmpty: false } ? dataFld.ToValue<decimal>() : 0m;
                                    oitem.SetField(fld.Name, fld.SchemeType?.CreateNode(orgVal + dataVal));
                                }
                            }
                        }
                    }
                    break;
                }
        }

        await context.SaveFieldDataAsync(fieldType, origin?.ToJson());
        await context.CommitTransactionAsync(!raiseEvent);
        return (origin is ArrayTypeNode { Count: < 2 } arrayNode ? arrayNode.FirstOrDefault() : origin)?.ToJson();

    ROLLBACK:
        await context.RollbackTransactionAsync();
        return null;
    }

    /// <summary>
    /// Save the app field data
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="app">The application</param>
    /// <param name="field">The field</param>
    /// <param name="data">The data</param>
    /// <param name="onlyAdd">Only add no update</param>
    /// <param name="target">The target</param>
    /// <param name="raiseEvent">Whether raise event</param>
    /// <param name="overrides">Override existed columns</param>
    /// <returns></returns>
    [Schema]
    [SideEffect]
    public static async Task<bool> saveappdata(
        SchemaContext context,
        [Schema(NS_SYSTEM_SCHEMA_APP)] string app,
        [Schema(NS_SYSTEM_SCHEMA_APP_FIELD)] string field,
        [Schema(NS_GENERIC_TYPE)] JsonNode data,
        bool onlyAdd,
        string target,
        bool raiseEvent = false,
        params string[] overrides
    )
    {
        if (string.IsNullOrEmpty(target)) return false;
        
        AppType? appType = !string.IsNullOrEmpty(app)
            ? await context.GetAppTypeAsync(app)
            : null;

        AppFieldType? fieldType = appType?.GetField(field);
        if (fieldType == null) return false;
        
        (_, AnySchemaNode? dataNode, JsonNode? error) = await fieldType.ValidateDataAsync(context, data);
        if (error != null || dataNode == null || dataNode.IsEmpty) return false;
        
        using var access = context.StackAccess(app, target);

        await context.BeginTransactionAsync();
        await context.SaveFieldDataAsync(fieldType, dataNode, onlyAdd: onlyAdd, overrides: overrides);
        await context.CommitTransactionAsync(!raiseEvent);
        return true;
    }
    
    #endregion
}