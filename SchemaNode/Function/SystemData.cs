using SchemaNode.Attribute;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Utility;
using System.Text.Json.Nodes;
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
    [NoCache]
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

        var (value, _) = await context.GetFieldDataAsync(fieldType, target);
        return value != null ? value.ToValue<T>() : default;
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

        JsonValue? jsonKey = JsonValue.Create(key);
        if (jsonKey == null || jsonKey.IsEmpty()) return default;
        
        AppType? appType = !string.IsNullOrEmpty(app)
            ? await context.GetAppTypeAsync(app)
            : null;

        AppFieldType? fieldType = appType?.GetField(field);
        if (fieldType is not { EnableDynamicTable: true } || fieldType.SchemaType is not ArrayType { Primary.Length: 1 } arrType) return default;

        JsonObject query = new()
        {
            [arrType.Primary[0]] = jsonKey
        };
        var (value, _) = await context.GetFieldDataAsync(fieldType, target, query);
        return value is ArrayTypeNode arrayNode && arrayNode.Count > 0 ? arrayNode.First().ToValue<T>() : default;
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

        JsonValue? jsonKey1 = JsonValue.Create(key1);
        JsonValue? jsonKey2 = JsonValue.Create(key2);
        if (jsonKey1 == null || jsonKey1.IsEmpty()) return default;
        if (jsonKey2 == null || jsonKey2.IsEmpty()) return default;
        
        AppType? appType = !string.IsNullOrEmpty(app)
            ? await context.GetAppTypeAsync(app)
            : null;

        AppFieldType? fieldType = appType?.GetField(field);
        if (fieldType is not { EnableDynamicTable: true } || fieldType.SchemaType is not ArrayType { Primary.Length: 2 } arrType) return default;

        JsonObject query = new()
        {
            [arrType.Primary[0]] = jsonKey1,
            [arrType.Primary[1]] = jsonKey2
        };
        var (value, _) = await context.GetFieldDataAsync(fieldType, target, query);
        return value is ArrayTypeNode arrayNode && arrayNode.Count > 0 ? arrayNode.First().ToValue<T>() : default;
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

        JsonValue? jsonKey1 = JsonValue.Create(key1);
        JsonValue? jsonKey2 = JsonValue.Create(key2);
        JsonValue? jsonKey3 = JsonValue.Create(key3);
        if (jsonKey1 == null || jsonKey1.IsEmpty()) return default;
        if (jsonKey2 == null || jsonKey2.IsEmpty()) return default;
        if (jsonKey3 == null || jsonKey3.IsEmpty()) return default;
        
        AppType? appType = !string.IsNullOrEmpty(app)
            ? await context.GetAppTypeAsync(app)
            : null;

        AppFieldType? fieldType = appType?.GetField(field);
        if (fieldType is not { EnableDynamicTable: true } || fieldType.SchemaType is not ArrayType { Primary.Length: 3 } arrType) return default;

        JsonObject query = new()
        {
            [arrType.Primary[0]] = jsonKey1,
            [arrType.Primary[1]] = jsonKey2,
            [arrType.Primary[2]] = jsonKey3
        };
        var (value, _) = await context.GetFieldDataAsync(fieldType, target, query);
        return value is ArrayTypeNode arrayNode && arrayNode.Count > 0 ? arrayNode.First().ToValue<T>() : default;
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

        JsonValue? jsonKey1 = JsonValue.Create(key1);
        JsonValue? jsonKey2 = JsonValue.Create(key2);
        JsonValue? jsonKey3 = JsonValue.Create(key3);
        JsonValue? jsonKey4 = JsonValue.Create(key4);
        if (jsonKey1 == null || jsonKey1.IsEmpty()) return default;
        if (jsonKey2 == null || jsonKey2.IsEmpty()) return default;
        if (jsonKey3 == null || jsonKey3.IsEmpty()) return default;
        if (jsonKey4 == null || jsonKey4.IsEmpty()) return default;
        
        AppType? appType = !string.IsNullOrEmpty(app)
            ? await context.GetAppTypeAsync(app)
            : null;

        AppFieldType? fieldType = appType?.GetField(field);
        if (fieldType is not { EnableDynamicTable: true } || fieldType.SchemaType is not ArrayType { Primary.Length: 4 } arrType) return default;

        JsonObject query = new()
        {
            [arrType.Primary[0]] = jsonKey1,
            [arrType.Primary[1]] = jsonKey2,
            [arrType.Primary[2]] = jsonKey3,
            [arrType.Primary[3]] = jsonKey4
        };
        var (value, _) = await context.GetFieldDataAsync(fieldType, target, query);
        return value is ArrayTypeNode arrayNode && arrayNode.Count > 0 ? arrayNode.First().ToValue<T>() : default;
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
    public static async Task<JsonNode?> incrappdata(
        SchemaContext context,
        [Schema(NS_SYSTEM_SCHEMA_APP)] string app,
        [Schema(NS_SYSTEM_SCHEMA_APP_FIELD)] string field,
        [Schema(NS_GENERIC_TYPE)] JsonNode data,
        string target
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
            || fieldType.SchemaType is StructType s && !s.Fields.Any(f => f.TypeNode is ScalarType {  IsNumber: true })
            || fieldType.SchemaType is ArrayType a && (a.ElementSchemaType is not StructType || a.Primary == null || a.Primary.Length == 0)
            ) return null;

        AnySchemaNode? dataNode = fieldType.SchemaType?.CreateNode(data);
        if (dataNode == null || dataNode.IsEmpty) return null;

        await context.BeginTransactionAsync();
        (AnySchemaNode? origin, _) = await context.GetFieldDataAsync(fieldType, target, dataNode.ToJson(), forUpdate: true);
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

                        if (orgFld?.Type is ScalarType { IsNumber: true } && dataFld?.Type is ScalarType { IsNumber: true })
                        {
                            decimal orgVal = orgFld is { IsEmpty: false } ? orgFld.ToValue<decimal>() : 0m;
                            decimal dataVal = dataFld is { IsEmpty: false } ? dataFld.ToValue<decimal>() : 0m;
                            originStruct.SetField(fld.Name, fld.TypeNode?.CreateNode(orgVal + dataVal));
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

                                if (orgFld?.Type is ScalarType { IsNumber: true } && dataFld?.Type is ScalarType { IsNumber: true })
                                {
                                    decimal orgVal = orgFld is { IsEmpty: false } ? orgFld.ToValue<decimal>() : 0m;
                                    decimal dataVal = dataFld is { IsEmpty: false } ? dataFld.ToValue<decimal>() : 0m;
                                    oitem.SetField(fld.Name, fld.TypeNode?.CreateNode(orgVal + dataVal));
                                }
                            }
                        }
                    }
                    break;
                }
        }

        await context.SaveFieldDataAsync(fieldType, target, origin?.ToJson());
        await context.CommitTransactionAsync();
        return (origin is ArrayTypeNode { Count: < 2 } arrayNode ? arrayNode.FirstOrDefault() : origin)?.ToJson();

    ROLLBACK:
        await context.RollbackTransactionAsync();
        return null;
    }

    [Schema]
    public static async Task<bool> saveappdata(
        SchemaContext context,
        [Schema(NS_SYSTEM_SCHEMA_APP)] string app,
        [Schema(NS_SYSTEM_SCHEMA_APP_FIELD)] string field,
        [Schema(NS_GENERIC_TYPE)] JsonNode data,
        bool onlyAdd,
        string target
    )
    {
        if (string.IsNullOrEmpty(target)) return false;

        AppType? appType = !string.IsNullOrEmpty(app)
            ? await context.GetAppTypeAsync(app)
            : null;

        AppFieldType? fieldType = appType?.GetField(field);
        AnySchemaNode? dataNode = fieldType?.SchemaType?.CreateNode(data);
        if (dataNode == null || dataNode.IsEmpty) return false;

        await context.BeginTransactionAsync();
        await context.SaveFieldDataAsync(fieldType!, target, dataNode, onlyAdd: onlyAdd);
        await context.CommitTransactionAsync();
        return true;
    }
    
    #endregion
}