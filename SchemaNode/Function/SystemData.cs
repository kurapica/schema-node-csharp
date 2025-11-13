using System.Text.Json.Nodes;
using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Function;

/// <summary>
/// The system.data api
/// </summary>
[SchemaType(NS_SYSTEM_DATA)]
public static class SystemData
{
    #region Get Application Data

    /// <summary>
    /// Gets the application data if single value
    /// </summary>
    [SchemaType]
    public static async Task<AnySchemaNode?> GetAppData(
        SchemaContext context,
        [SchemaType(NS_SYSTEM_SCHEMA_APP)] string app,
        [SchemaType(NS_SYSTEM_SCHEMA_APP_FIELD)]
        string field)
    {
        string target = string.IsNullOrEmpty(context.Target)
            ? Guid.Empty.ToString()
            : context.Target;

        AppType? appType = !string.IsNullOrEmpty(app)
            ? await context.GetAppTypeAsync(app)
            : null;

        AppFieldType? fieldType = appType?.GetField(field);
        if (fieldType is not { EnableDynamicTable: true } || !fieldType.Single) return null;

        var (value, _) = await context.GetFieldDataAsync(fieldType, target);
        return value;
    }
    
    /// <summary>
    /// Gets the application data by one key
    /// </summary>
    [SchemaType]
    public static async Task<AnySchemaNode?> GetAppDataByOneKey<T1>(
        SchemaContext context,
        [SchemaType(NS_SYSTEM_SCHEMA_APP)] string app,
        [SchemaType(NS_SYSTEM_SCHEMA_APP_FIELD)]
        string field,
        T1 key
        )
    {
        JsonValue? jsonKey = JsonValue.Create(key);
        if (jsonKey == null || jsonKey.IsEmpty()) return null;
        
        string target = string.IsNullOrEmpty(context.Target)
            ? Guid.Empty.ToString()
            : context.Target;

        AppType? appType = !string.IsNullOrEmpty(app)
            ? await context.GetAppTypeAsync(app)
            : null;

        AppFieldType? fieldType = appType?.GetField(field);
        if (fieldType is not { EnableDynamicTable: true } || fieldType.SchemaType is not ArrayType { Primary.Length: 1 } arrType) return null;

        JsonObject query = new()
        {
            [arrType.Primary[0]] = jsonKey
        };
        var (value, _) = await context.GetFieldDataAsync(fieldType, target, query);
        return value is ArrayTypeNode arrayNode ? arrayNode.FirstOrDefault() : null;
    }

    /// <summary>
    /// Gets the application data by one key
    /// </summary>
    [SchemaType]
    public static async Task<AnySchemaNode?> GetAppDataByTwoKey<T1, T2>(
        SchemaContext context,
        [SchemaType(NS_SYSTEM_SCHEMA_APP)] string app,
        [SchemaType(NS_SYSTEM_SCHEMA_APP_FIELD)]
        string field,
        T1 key1, T2 key2
    )
    {
        JsonValue? jsonKey1 = JsonValue.Create(key1);
        JsonValue? jsonKey2 = JsonValue.Create(key2);
        if (jsonKey1 == null || jsonKey1.IsEmpty()) return null;
        if (jsonKey2 == null || jsonKey2.IsEmpty()) return null;
        
        string target = string.IsNullOrEmpty(context.Target)
            ? Guid.Empty.ToString()
            : context.Target;

        AppType? appType = !string.IsNullOrEmpty(app)
            ? await context.GetAppTypeAsync(app)
            : null;

        AppFieldType? fieldType = appType?.GetField(field);
        if (fieldType is not { EnableDynamicTable: true } || fieldType.SchemaType is not ArrayType { Primary.Length: 2 } arrType) return null;

        JsonObject query = new()
        {
            [arrType.Primary[0]] = jsonKey1,
            [arrType.Primary[1]] = jsonKey2
        };
        var (value, _) = await context.GetFieldDataAsync(fieldType, target, query);
        return value is ArrayTypeNode arrayNode ? arrayNode.FirstOrDefault() : null;
    }

    /// <summary>
    /// Gets the application data by one key
    /// </summary>
    [SchemaType]
    public static async Task<AnySchemaNode?> GetAppDataByThreeKey<T1, T2, T3>(
        SchemaContext context,
        [SchemaType(NS_SYSTEM_SCHEMA_APP)] string app,
        [SchemaType(NS_SYSTEM_SCHEMA_APP_FIELD)]
        string field,
        T1 key1, T2 key2, T3 key3
    )
    {
        JsonValue? jsonKey1 = JsonValue.Create(key1);
        JsonValue? jsonKey2 = JsonValue.Create(key2);
        JsonValue? jsonKey3 = JsonValue.Create(key3);
        if (jsonKey1 == null || jsonKey1.IsEmpty()) return null;
        if (jsonKey2 == null || jsonKey2.IsEmpty()) return null;
        if (jsonKey3 == null || jsonKey3.IsEmpty()) return null;
        
        string target = string.IsNullOrEmpty(context.Target)
            ? Guid.Empty.ToString()
            : context.Target;

        AppType? appType = !string.IsNullOrEmpty(app)
            ? await context.GetAppTypeAsync(app)
            : null;

        AppFieldType? fieldType = appType?.GetField(field);
        if (fieldType is not { EnableDynamicTable: true } || fieldType.SchemaType is not ArrayType { Primary.Length: 3 } arrType) return null;

        JsonObject query = new()
        {
            [arrType.Primary[0]] = jsonKey1,
            [arrType.Primary[1]] = jsonKey2,
            [arrType.Primary[2]] = jsonKey3
        };
        var (value, _) = await context.GetFieldDataAsync(fieldType, target, query);
        return value is ArrayTypeNode arrayNode ? arrayNode.FirstOrDefault() : null;
    }

    /// <summary>
    /// Gets the application data by one key
    /// </summary>
    [SchemaType]
    public static async Task<AnySchemaNode?> GetAppDataByFourKey<T1, T2, T3, T4>(
        SchemaContext context,
        [SchemaType(NS_SYSTEM_SCHEMA_APP)] string app,
        [SchemaType(NS_SYSTEM_SCHEMA_APP_FIELD)]
        string field,
        T1 key1, T2 key2, T3 key3, T4 key4
    )
    {
        JsonValue? jsonKey1 = JsonValue.Create(key1);
        JsonValue? jsonKey2 = JsonValue.Create(key2);
        JsonValue? jsonKey3 = JsonValue.Create(key3);
        JsonValue? jsonKey4 = JsonValue.Create(key4);
        if (jsonKey1 == null || jsonKey1.IsEmpty()) return null;
        if (jsonKey2 == null || jsonKey2.IsEmpty()) return null;
        if (jsonKey3 == null || jsonKey3.IsEmpty()) return null;
        if (jsonKey4 == null || jsonKey4.IsEmpty()) return null;
        
        string target = string.IsNullOrEmpty(context.Target)
            ? Guid.Empty.ToString()
            : context.Target;

        AppType? appType = !string.IsNullOrEmpty(app)
            ? await context.GetAppTypeAsync(app)
            : null;

        AppFieldType? fieldType = appType?.GetField(field);
        if (fieldType is not { EnableDynamicTable: true } || fieldType.SchemaType is not ArrayType { Primary.Length: 4 } arrType) return null;

        JsonObject query = new()
        {
            [arrType.Primary[0]] = jsonKey1,
            [arrType.Primary[1]] = jsonKey2,
            [arrType.Primary[2]] = jsonKey3,
            [arrType.Primary[3]] = jsonKey4
        };
        var (value, _) = await context.GetFieldDataAsync(fieldType, target, query);
        return value is ArrayTypeNode arrayNode ? arrayNode.FirstOrDefault() : null;
    }

    #endregion
}