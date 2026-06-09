using SchemaNode.Attribute;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using System.Text.Json.Nodes;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global

namespace SchemaNode.Function;

/// <summary>
/// The system.data api
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_DATA)]
public static class SystemData
{
    #region Context Item

    /// <summary>
    /// Gets the context item
    /// </summary>
    public static DataNode? getcontext(SchemaContext context, string item)
    {
        
    }

    #endregion

    #region Get single Application Data

    /// <summary>
    /// Gets the app data with full primary keys
    /// </summary>
    [Schema]
    public static async Task<T?> get<T>(
        SchemaContext context,
        [Schema(NS_SYSTEM_SCHEMA_DOMAIN_APP)] string app,
        [Schema(NS_SYSTEM_SCHEMA_DOMAIN_FIELD)] string field,
        params object?[] args)
    {
        // get the app field type
        AppType? appType = !string.IsNullOrEmpty(app) ? await context.GetAppTypeAsync(app) : null;
        AppFieldType? fieldType = appType?.GetField(field);
        if (fieldType is not { EnableDynamicTable: true }) return default;

        ArrayType? arrType = fieldType.ValueType as ArrayType;
        string[] keys = arrType?.Primary ?? [];

        // full primary key contains
        if (keys.Length != args.Length) return default;

        // Check the app access is contains, only allow access in the same app or system depth app
        Access? access = context.GetSchemaContextItem<Access>();
        if ((access == null || !app.Equals(access.App)) && appType!.ScopeType != Enum.AppScopeType.SystemLevel)
            return default;

        // get the key type
        AppSchemaDataFilter? filter = null;
        List<DataNode>[] keyValues = new List<DataNode>[keys.Length];
        for (int i = 0; i < keys.Length; i++)
        {
            AnySchemaType? keyType = (arrType!.ElementSchemaType as StructType)?.GetField(keys[i])?.SchemaType;
            DataNode? valueNode = keyType?.CreateNode(args[i]);
            if (valueNode == null || valueNode.IsEmpty) return default;

            if (keyType is EnumType { Cascade: { Length: > 1 } } enumType &&
                fieldType.Filters != null &&
                fieldType.Filters.Any(f =>
                    f.Filter.Equals(keys[i], StringComparison.OrdinalIgnoreCase) &&
                    f.Resolve == Enum.FieldFilterResolve.CascadeParent))
            {
                EnumValueAccess[] enumAccess = await enumType.LoadEnumAccessListAsync(context, valueNode.ToString(),
                    noSubList: true, withSubList: false);
                if (enumAccess.Length == 0) return default;
                keyValues[i] = enumAccess.Select(a => keyType.CreateNode(a.Value)!).ToList();
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

        (DataNode? value, _) = await context.GetAppFieldDataAsync(fieldType, keys.Length == 0 ? AppSchemaDataResult.First : AppSchemaDataResult.List, filter);
        DataNode? result = value;
        if (keys.Length > 0)
        {
            if (value is not ArrayTypeNode { Count: > 0 } arr) return default;

            // find the contains item
            StructNode[] items = arr.Cast<StructNode>().ToArray();
            for (int i = 0; i < keyValues.Length; i++)
            {
                if (keyValues[i].Count == 1) continue;

                // contains the last
                for (int j = keyValues[i].Count - 1; j >= 0; j--)
                {
                    DataNode key = keyValues[i][j];
                    if (!items.Any(n => n.GetField(keys[i]) is { } f && key.Equals(f))) continue;
                    items = items.Where(n => n.GetField(keys[i]) is { } f && key.Equals(f)).ToArray();
                    break;
                }
            }
            result = items.Length > 0 ? items[0] : null;
        }
        return result != null && !result.IsEmpty ? result.ToValue<T>() : default;
    }
    
    #endregion

    #region Get Application Data For field
        
    /// <summary>
    /// Gets the application data for field if single value
    /// </summary>
    [Schema]
    public static async Task<T?> getfield<T>(
        SchemaContext context,
        [Schema(NS_SYSTEM_SCHEMA_DOMAIN_APP)] string app,
        [Schema(NS_SYSTEM_SCHEMA_DOMAIN_FIELD)] string field,
        string dataField,
        params object?[] args) where T : struct
    {
        DataNode? result = await get<DataNode>(context, app, field, args);
        DataNode? f = (result as StructNode)?.GetField(dataField);
        return f != null ? f.ToValue<T>() : null;
    }
    
    #endregion

    #region Data Source

    /// <summary>
    /// Generate a data source for the app field, waiting for query, the codes won't be execution unless use it in wrong way
    /// </summary>
    [Schema]
    public static async Task<ArrayTypeNode> getdatasource(
        SchemaContext context,
        [Schema(NS_SYSTEM_SCHEMA_DOMAIN_APP)] string app,
        [Schema(NS_SYSTEM_SCHEMA_DOMAIN_FIELD)] string field)
    {
        AppType? appType = !string.IsNullOrEmpty(app)
            ? await context.GetAppTypeAsync(app)
            : null;

        AppFieldType? fieldType = appType?.GetField(field);
        if (fieldType?.ValueType == null) throw new InvalidOperationException($"The field {field} not found in the app {app}.");
        if (fieldType.ValueType is not ArrayType) throw new InvalidOperationException($"The field {field} type is not array type in the app {app}.");
        return new ArrayTypeNode(fieldType.ValueType);
    }

    #endregion
    
    #region Write App Data

    /// <summary>
    /// Incr the app field data
    /// </summary>
    [Schema]
    [SideEffect]
    [WorkflowOnly]
    public static async Task<JsonNode?> incr(
        SchemaContext context,
        [Schema(NS_SYSTEM_SCHEMA_DOMAIN_APP)] string app,
        [Schema(NS_SYSTEM_SCHEMA_DOMAIN_FIELD)] string field,
        [Schema(NS_GENERIC_TYPE)] JsonNode data,
        bool raiseEvent = false
    )
    {
        AppType? appType = !string.IsNullOrEmpty(app) ? await context.GetAppTypeAsync(app) : null;
        if (appType == null) return null;

        Access? access = context.GetSchemaContextItem<Access>();
        string? target = access?.Target;

        // Check the app access is contains, only allow access in the same app or system depth app
        if ((access == null || !app.Equals(access.App)) && appType.ScopeType != Enum.AppScopeType.SystemLevel) return null;
        if (string.IsNullOrEmpty(target) && appType.ScopeType != Enum.AppScopeType.SystemLevel) return null;

        AppFieldType? fieldType = appType.GetField(field);
        if (fieldType == null 
            || fieldType.ValueType is EnumType 
            || fieldType.ValueType is ScalarType { IsNumber: false } 
            || fieldType.ValueType is StructType s && !s.Fields.Any(f => f.SchemaType is ScalarType {  IsNumber: true })
            || fieldType.ValueType is ArrayType a && (a.ElementSchemaType is not StructType || a.Primary == null || a.Primary.Length == 0)
            ) return null;

        DataNode? dataNode = fieldType.ValueType?.CreateNode(data);
        if (dataNode == null || dataNode.IsEmpty) return null;

        using var stack = context.StackAccess(appType!.Name, target);
        await context.BeginTransactionAsync();
        DataNode? origin = await context.GetAppFieldDataAsync(fieldType, dataNode, forUpdate: true);
        if (origin == null) goto ROLLBACK;

        switch (fieldType.ValueType)
        {
            case ScalarType:
                {
                    if (dataNode is not ScalarTypeNode) goto ROLLBACK;
                    origin = fieldType.ValueType.CreateNode(
                        (origin is { IsEmpty: false } ? origin.ToValue<decimal>() : 0m) +
                        (dataNode is { IsEmpty: false } ? dataNode.ToValue<decimal>() : 0m)
                    );
                    break;
                }
            case StructType @struct:
                {
                    if (dataNode is not StructNode structData || origin is not StructNode originStruct) goto ROLLBACK;
                    foreach (var fld in @struct.Fields)
                    {
                        DataNode? orgFld = originStruct.GetField(fld.Name);
                        DataNode? dataFld = structData.GetField(fld.Name);

                        if (orgFld?.SchemaType is ScalarType { IsNumber: true } && dataFld?.SchemaType is ScalarType { IsNumber: true })
                        {
                            decimal orgVal = orgFld is { IsEmpty: false } ? orgFld.ToValue<decimal>() : 0m;
                            decimal dataVal = dataFld is { IsEmpty: false } ? dataFld.ToValue<decimal>() : 0m;
                            originStruct.SetField(fld.Name, fld.SchemaType?.CreateNode(orgVal + dataVal));
                        }
                    }
                    break;
                }
            case ArrayType arrType:
                {
                    if (dataNode is not ArrayTypeNode arrayData || origin is not ArrayTypeNode originArray) goto ROLLBACK;
                    Dictionary<string, StructNode> arrDict = new();
                    foreach(var i in arrayData)
                    {
                        var ditem = (StructNode)i;
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
                        var oitem = (StructNode)i;
                        string? pkey = arrType.GetPrimaryKey(oitem);
                        if (pkey != null && arrDict.TryGetValue(pkey, out StructNode? ditem))
                        {
                            foreach (var fld in arrStruct.Fields)
                            {
                                DataNode? orgFld = oitem.GetField(fld.Name);
                                DataNode? dataFld = ditem.GetField(fld.Name);

                                if (orgFld?.SchemaType is ScalarType { IsNumber: true } && dataFld?.SchemaType is ScalarType { IsNumber: true })
                                {
                                    decimal orgVal = orgFld is { IsEmpty: false } ? orgFld.ToValue<decimal>() : 0m;
                                    decimal dataVal = dataFld is { IsEmpty: false } ? dataFld.ToValue<decimal>() : 0m;
                                    oitem.SetField(fld.Name, fld.SchemaType?.CreateNode(orgVal + dataVal));
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
    [WorkflowOnly]
    public static async Task<bool> save(
        SchemaContext context,
        [Schema(NS_SYSTEM_SCHEMA_DOMAIN_APP)] string app,
        [Schema(NS_SYSTEM_SCHEMA_DOMAIN_FIELD)] string field,
        [Schema(NS_GENERIC_TYPE)] JsonNode data,
        bool onlyAdd,
        bool raiseEvent = false,
        params string[] overrides
    )
    {
        AppType? appType = !string.IsNullOrEmpty(app) ? await context.GetAppTypeAsync(app): null;
        if (appType == null) return false;

        Access? access = context.GetSchemaContextItem<Access>();
        string? target = access?.Target;

        // Check the app access is contains, only allow access in the same app or system depth app
        if ((access == null || !app.Equals(access.App)) && appType.ScopeType != Enum.AppScopeType.SystemLevel) return false;
        if (string.IsNullOrEmpty(target) && appType.ScopeType != Enum.AppScopeType.SystemLevel) return false;

        AppFieldType? fieldType = appType?.GetField(field);
        if (fieldType == null) return false;
        
        (_, DataNode? dataNode, JsonNode? error) = await fieldType.ValidateDataAsync(context, data);
        if (error != null || dataNode == null || dataNode.IsEmpty) return false;
        
        using var stack = context.StackAccess(app, target);

        await context.BeginTransactionAsync();
        await context.SaveFieldDataAsync(fieldType, dataNode, onlyAdd: onlyAdd, overrides: overrides);
        await context.CommitTransactionAsync(!raiseEvent);
        return true;
    }

    /// <summary>
    /// Delete the data
    /// </summary>
    /// <param name="context"></param>
    /// <param name="app"></param>
    /// <param name="field"></param>
    /// <param name="data"></param>
    /// <param name="raiseEvent"></param>
    /// <returns></returns>
    [Schema]
    [SideEffect]
    [WorkflowOnly]
    public static async Task<bool> delete(
        SchemaContext context,
        [Schema(NS_SYSTEM_SCHEMA_DOMAIN_APP)] string app,
        [Schema(NS_SYSTEM_SCHEMA_DOMAIN_FIELD)] string field,
        [Schema(NS_GENERIC_TYPE)] JsonNode data,
        bool raiseEvent = false
    )
    {
        AppType? appType = !string.IsNullOrEmpty(app) ? await context.GetAppTypeAsync(app) : null;
        if (appType == null) return false;

        Access? access = context.GetSchemaContextItem<Access>();
        string? target = access?.Target;

        // Check the app access is contains, only allow access in the same app or system depth app
        if ((access == null || !app.Equals(access.App)) && appType.ScopeType != Enum.AppScopeType.SystemLevel) return false;
        if (string.IsNullOrEmpty(target) && appType.ScopeType != Enum.AppScopeType.SystemLevel) return false;

        AppFieldType? fieldType = appType?.GetField(field);
        if (fieldType == null) return false;

        (_, DataNode? dataNode, JsonNode? error) = await fieldType.ValidateDataAsync(context, data);
        if (error != null || dataNode == null || dataNode.IsEmpty) return false;

        using var stack = context.StackAccess(app, target);

        await context.BeginTransactionAsync();
        if (fieldType.ValueType is ArrayType { Primary: {  Length: > 0 }})
            await context.DeleteFieldListDataAsync(fieldType, dataNode);
        else
            await context.SaveFieldDataAsync(fieldType, null);
        await context.CommitTransactionAsync(!raiseEvent);
        return true;
    }

    #endregion
}