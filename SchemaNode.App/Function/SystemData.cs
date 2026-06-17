using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using System.Text.Json.Nodes;
using SchemaNode.Data;
using SchemaNode.Enum;
using SchemaNode.Property.Core;
using SchemaNode.Property.Function;
using SchemaNode.Scalar;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
using AppType = SchemaNode.Runtime.AppType;
using ArrayType = SchemaNode.Runtime.ArrayType;
using DecimalType = SchemaNode.Runtime.DecimalType;
using EnumType = SchemaNode.Runtime.EnumType;
using IntType = SchemaNode.Runtime.IntType;
using StructType = SchemaNode.Runtime.StructType;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global

namespace SchemaNode.Function;

/// <summary>
/// The system.data api
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_DATA)]
public static class SystemData
{
    #region Get single Application Data

    /// <summary>
    /// Gets the app data with full primary keys
    /// </summary>
    public static async Task<T?> get<T>(
        SchemaContext context,
        [Meta<SchemaType>(typeof(Schema.AppType))] string app,
        [Meta<SchemaType>(typeof(Identifier))] string field,
        params object?[] args)
    {
        // get the app field type
        AppType? appType = !string.IsNullOrEmpty(app) ? await context.GetAppTypeAsync(app) : null;
        AppFieldType? fieldType = appType?.GetField(field);
        if (fieldType is not { EnableDynamicTable: true }) return default;

        ArrayType? arrType = fieldType.ValueType as ArrayType;
        var keys = arrType?.Primary ?? [];

        // full primary key contains
        if (keys.Count != args.Length) return default;

        // Check the app access is contained, only allow access in the same app or system depth app
        Access? access = context.GetContextItem<Access>();
        if ((access == null || !app.Equals(access.App)) && appType!.ScopeType != AppScopeType.SystemLevel)
            return default;

        // get the key type
        AppSchemaDataFilter? filter = null;
        List<DataNode>[] keyValues = new List<DataNode>[keys.Count];
        for (int i = 0; i < keys.Count; i++)
        {
            var keyType = (arrType!.Element as StructType)?.GetField(keys[i])?.Type;
            DataNode? valueNode = keyType?.From(args[i]);
            if (valueNode == null || valueNode.IsEmpty) return default;

            if (keyType is EnumType { Cascade: { Length: > 1 } } enumType &&
                fieldType.Filters != null &&
                fieldType.Filters.Any(f =>
                    f.Filter.Equals(keys[i], StringComparison.OrdinalIgnoreCase) &&
                    f.Resolve == FieldFilterResolve.CascadeParent))
            {
                EnumValueAccess[] enumAccess = await enumType.LoadEnumAccessListAsync(context, valueNode.GetValue<string>()!,
                    noSubList: true, withSubList: false);
                if (enumAccess.Length == 0) return default;
                keyValues[i] = enumAccess.Select(a => keyType.From(a.Value)).ToList();
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

        (DataNode? value, _) = await context.GetAppFieldDataAsync(fieldType, keys.Count == 0 ? AppSchemaDataResult.First : AppSchemaDataResult.List, filter);
        DataNode? result = value;
        if (keys.Count > 0)
        {
            if (value is not ArrayNode { Count: > 0 } arr) return default;

            // find the contains item
            StructNode[] items = arr.Cast<StructNode>().ToArray();
            for (int i = 0; i < keyValues.Length; i++)
            {
                if (keyValues[i].Count == 1) continue;

                // contains the last
                for (int j = keyValues[i].Count - 1; j >= 0; j--)
                {
                    DataNode key = keyValues[i][j];
                    if (!items.Any(n => n.GetAccessValue(keys[i]) is { } f && key.Equals(f))) continue;
                    items = items.Where(n => n.GetAccessValue(keys[i]) is { } f && key.Equals(f)).ToArray();
                    break;
                }
            }
            result = items.Length > 0 ? items[0] : null;
        }
        return result is { IsEmpty: false } ? result.GetValue<T>() : default;
    }
    
    #endregion

    #region Get Application Data For field
        
    /// <summary>
    /// Gets the application data for field if single value
    /// </summary>
    public static async Task<T?> getfield<T>(
        SchemaContext context,
        [Meta<SchemaType>(typeof(Schema.AppType))] string app,
        [Meta<SchemaType>(typeof(Identifier))] string field,
        string dataField,
        params object?[] args) where T : struct
    {
        DataNode? result = await get<DataNode>(context, app, field, args);
        DataNode? f = (result as StructNode)?.GetAccessValue(dataField);
        return f != null ? f.GetValue<T>() : null;
    }
    
    #endregion

    #region Data Source

    /// <summary>
    /// Generate a data source for the app field, waiting for query, the codes won't be execution unless use it in wrong way
    /// </summary>
    public static async Task<ArrayNode> getdatasource(
        SchemaContext context,
        [Meta<SchemaType>(typeof(Schema.AppType))] string app,
        [Meta<SchemaType>(typeof(Identifier))] string field)
    {
        AppType? appType = !string.IsNullOrEmpty(app)
            ? await context.GetAppTypeAsync(app)
            : null;

        AppFieldType? fieldType = appType?.GetField(field);
        if (fieldType?.ValueType == null) throw new InvalidOperationException($"The field {field} not found in the app {app}.");
        if (fieldType.ValueType is not ArrayType) throw new InvalidOperationException($"The field {field} type is not array type in the app {app}.");
        return new ArrayNode(fieldType.ValueType);
    }

    #endregion
    
    #region Write App Data

    /// <summary>
    /// Incr the app field data
    /// </summary>
    [Meta<SideEffect>(true)]
    [Meta<WorkflowOnly>(true)]
    public static async Task<JsonNode?> incr<T>(
        SchemaContext context,
        [Meta<SchemaType>(typeof(Schema.AppType))] string app,
        [Meta<SchemaType>(typeof(Identifier))] string field,
        T data,
        bool raiseEvent = false
    )
    {
        AppType? appType = !string.IsNullOrEmpty(app) ? await context.GetAppTypeAsync(app) : null;
        if (appType == null) return null;

        Access? access = context.GetContextItem<Access>();
        string? target = access?.Target;

        // Check the app access is contained, only allow access in the same app or system depth app
        if ((access == null || !app.Equals(access.App)) && appType.ScopeType != AppScopeType.SystemLevel) return null;
        if (string.IsNullOrEmpty(target) && appType.ScopeType != AppScopeType.SystemLevel) return null;

        AppFieldType? fieldType = appType.GetField(field);
        if (fieldType == null 
            || fieldType.ValueType is EnumType 
            || fieldType.ValueType is ScalarType and not IntType and not DecimalType 
            || fieldType.ValueType is StructType s && !s.GetFields().Any(f => f.Type is IntType or DecimalType)
            || fieldType.ValueType is ArrayType a && (a.Element is not StructType || a.Primary == null || a.Primary.Count == 0)
            ) return null;

        DataNode? dataNode = fieldType.ValueType?.From(data);
        if (dataNode == null || dataNode.IsEmpty) return null;

        using var stack = context.StackAccess(appType.Name, target);
        await context.BeginTransactionAsync();
        DataNode? origin = await context.GetAppFieldDataAsync(fieldType, dataNode, forUpdate: true);
        if (origin == null) goto ROLLBACK;

        switch (fieldType.ValueType)
        {
            case ScalarType:
                {
                    if (dataNode is not IntNode and not NumericNode) goto ROLLBACK;
                    if (dataNode is IntNode)
                        origin = fieldType.ValueType.From(
                            (origin is { IsEmpty: false } ? origin.GetValue<long>() : 0m) +
                            (dataNode is { IsEmpty: false } ? dataNode.GetValue<long>() : 0m)
                        );
                    else if (dataNode is NumericNode)
                        origin = fieldType.ValueType.From(
                            (origin is { IsEmpty: false } ? origin.GetValue<decimal>() : 0m) +
                            (dataNode is { IsEmpty: false } ? dataNode.GetValue<decimal>() : 0m)
                        );
                    else
                        goto ROLLBACK;
                    break;
                }
            case StructType @struct:
                {
                    if (dataNode is not StructNode structData || origin is not StructNode originStruct) goto ROLLBACK;
                    foreach (var fld in @struct.GetFields())
                    {
                        DataNode? orgFld = originStruct.GetAccessValue(fld.Name);
                        DataNode? dataFld = structData.GetAccessValue(fld.Name);

                        if (orgFld?.Type is DecimalType && dataFld?.Type is DecimalType)
                        {
                            decimal orgVal = orgFld is { IsEmpty: false } ? orgFld.GetValue<decimal>() : 0m;
                            decimal dataVal = dataFld is { IsEmpty: false } ? dataFld.GetValue<decimal>() : 0m;
                            originStruct.TrySetFieldValue(fld.Name, fld.Type?.From(orgVal + dataVal));
                        }
                        else if (orgFld?.Type is IntType && dataFld?.Type is IntType)
                        {
                            long orgVal = orgFld is { IsEmpty: false } ? orgFld.GetValue<long>() : 0L;
                            long dataVal = dataFld is { IsEmpty: false } ? dataFld.GetValue<long>() : 0L;
                            originStruct.TrySetFieldValue(fld.Name, fld.Type?.From(orgVal + dataVal));
                        }
                    }
                    break;
                }
            case ArrayType arrType:
                {
                    if (dataNode is not ArrayNode arrayData || origin is not ArrayNode originArray) goto ROLLBACK;
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
                    StructType arrStruct = arrType.Element as StructType 
                        ?? throw new InvalidOperationException("Array element type is not struct type.");
                    foreach (var i in originArray)
                    {
                        var oitem = (StructNode)i;
                        string? pkey = arrType.GetPrimaryKey(oitem);
                        if (pkey != null && arrDict.TryGetValue(pkey, out StructNode? ditem))
                        {
                            foreach (var fld in arrStruct.GetFields())
                            {
                                DataNode? orgFld = oitem.GetAccessValue(fld.Name);
                                DataNode? dataFld = ditem.GetAccessValue(fld.Name);

                                if (orgFld?.Type is DecimalType && dataFld?.Type is DecimalType)
                                {
                                    decimal orgVal = orgFld is { IsEmpty: false } ? orgFld.GetValue<decimal>() : 0m;
                                    decimal dataVal = dataFld is { IsEmpty: false } ? dataFld.GetValue<decimal>() : 0m;
                                    oitem.TrySetFieldValue(fld.Name, fld.Type?.From(orgVal + dataVal));
                                }
                                else if (orgFld?.Type is IntType && dataFld?.Type is IntType)
                                {
                                    long orgVal = orgFld is { IsEmpty: false } ? orgFld.GetValue<long>() : 0L;
                                    long dataVal = dataFld is { IsEmpty: false } ? dataFld.GetValue<long>() : 0L;
                                    oitem.TrySetFieldValue(fld.Name, fld.Type?.From(orgVal + dataVal));
                                }
                            }
                        }
                    }
                    break;
                }
        }

        await context.SaveFieldDataAsync(fieldType, origin.ToJson());
        await context.CommitTransactionAsync(!raiseEvent);
        return (origin is ArrayNode { Count: < 2 } arrayNode ? arrayNode.FirstOrDefault() : origin)?.ToJson();

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
    /// <param name="raiseEvent">Whether raise event</param>
    /// <param name="overrides">Override existed columns</param>
    /// <returns></returns>
    [Meta<SideEffect>(true)]
    [Meta<WorkflowOnly>(true)]
    public static async Task<bool> save<T>(
        SchemaContext context,
        [Meta<SchemaType>(typeof(Schema.AppType))] string app,
        [Meta<SchemaType>(typeof(Identifier))] string field,
        T data,
        bool onlyAdd,
        bool raiseEvent = false,
        params string[] overrides
    )
    {
        AppType? appType = !string.IsNullOrEmpty(app) ? await context.GetAppTypeAsync(app): null;
        if (appType == null) return false;

        Access? access = context.GetContextItem<Access>();
        string? target = access?.Target;

        // Check the app access is contained, only allow access in the same app or system depth app
        if ((access == null || !app.Equals(access.App)) && appType.ScopeType != AppScopeType.SystemLevel) return false;
        if (string.IsNullOrEmpty(target) && appType.ScopeType != AppScopeType.SystemLevel) return false;

        AppFieldType? fieldType = appType.GetField(field);
        if (fieldType == null) return false;
        
        DataNode? dataNode = await fieldType.ValidateDataAsync(context, data);
        if (dataNode == null || dataNode.IsEmpty || !dataNode.IsValid) return false;
        
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
    [Meta<SideEffect>(true)]
    [Meta<WorkflowOnly>(true)]
    public static async Task<bool> delete<T>(
        SchemaContext context,
        [Meta<SchemaType>(typeof(Schema.AppType))] string app,
        [Meta<SchemaType>(typeof(Identifier))] string field,
        T data,
        bool raiseEvent = false
    )
    {
        AppType? appType = !string.IsNullOrEmpty(app) ? await context.GetAppTypeAsync(app) : null;
        if (appType == null) return false;

        Access? access = context.GetContextItem<Access>();
        string? target = access?.Target;

        // Check the app access is contained, only allow access in the same app or system depth app
        if ((access == null || !app.Equals(access.App)) && appType.ScopeType != AppScopeType.SystemLevel) return false;
        if (string.IsNullOrEmpty(target) && appType.ScopeType != AppScopeType.SystemLevel) return false;

        AppFieldType? fieldType = appType.GetField(field);
        if (fieldType == null) return false;

        DataNode? dataNode = await fieldType.ValidateDataAsync(context, data);
        if (dataNode == null || dataNode.IsEmpty || !dataNode.IsValid) return false;

        using var stack = context.StackAccess(app, target);

        await context.BeginTransactionAsync();
        if (fieldType.ValueType is ArrayType { Primary: { Count: > 0 }})
            await context.DeleteFieldListDataAsync(fieldType, dataNode);
        else
            await context.SaveFieldDataAsync(fieldType, null);
        await context.CommitTransactionAsync(!raiseEvent);
        return true;
    }

    #endregion
}