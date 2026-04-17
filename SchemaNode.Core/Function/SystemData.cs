using SchemaNode.Attribute;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;
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
    [Schema($"{NS_SYSTEM_DATA}.enum")]
    public static class EnumOper
    {
        /// <summary>
        /// Check the value is descendant of the root value
        /// </summary>
        [Schema]
        public static async Task<bool> isdescendant(SchemaContext context, [Schema(NS_SYSTEM_SCHEMA_TYPE_ENUM)] string @enum, string value, string root)
        {
            value = value.Trim();
            root = root.Trim();
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(root)) return false;
            if (value.Equals(root)) return true;

            // Check with value access
            EnumType? enumType = await context.GetSchemaTypeAsync<EnumType>(@enum);
            if (enumType == null) return false;
            EnumValueAccess[] access = await enumType.LoadEnumAccessListAsync(context, value, noSubList: true);
            return access.Any(a => a.Value.Equals(root));
        }

        /// <summary>
        /// Check the value is descendant of any root value
        /// </summary>
        [Schema]
        public static async Task<bool> isdescendantany(SchemaContext context, [Schema(NS_SYSTEM_SCHEMA_TYPE_ENUM)] string @enum, string value, string[] roots)
        {
            value = value.Trim();
            var rootSet = new HashSet<string>(roots.Select(r => r.Trim()));
            if (string.IsNullOrWhiteSpace(value) || roots.Length == 0) return false;
            if (roots.Any(r => r.Equals(value))) return true;

            // Check with value access
            EnumType? enumType = await context.GetSchemaTypeAsync<EnumType>(@enum);
            if (enumType == null) return false;
            EnumValueAccess[] access = await enumType.LoadEnumAccessListAsync(context, value, noSubList: true);
            return access.Any(a => rootSet.Contains(a.Value));
        }

        /// <summary>
        /// Gets the enum value's root with the given depth, if -1 means the last root, the root is 0, if the depth is bigger than the actual depth, return empty string
        /// </summary>
        [Schema]
        public static async Task<string> parent(SchemaContext context, [Schema(NS_SYSTEM_SCHEMA_TYPE_ENUM)] string @enum, string value, int depth = 0)
        {
            value = value.Trim();
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            // Check with value access
            EnumType? enumType = await context.GetSchemaTypeAsync<EnumType>(@enum);
            if (enumType == null) return string.Empty;
            EnumValueAccess[] access = await enumType.LoadEnumAccessListAsync(context, value, noSubList: true);
            return depth < 0 
                ? access.Length > 1-depth ? access[access.Length + depth - 1].Value : string.Empty
                : access.Length > depth ? access[depth].Value : string.Empty;
        }

        /// <summary>
        /// Gets the enum value's depth, the root is 0
        /// </summary>
        [Schema]
        public static async Task<long> depth(SchemaContext context, [Schema(NS_SYSTEM_SCHEMA_TYPE_ENUM)] string @enum, string value)
        {
            value = value.Trim();
            if (string.IsNullOrWhiteSpace(value)) return -1;
            // Check with value access
            EnumType? enumType = await context.GetSchemaTypeAsync<EnumType>(@enum);
            if (enumType == null) return -1;
            EnumValueAccess[] access = await enumType.LoadEnumAccessListAsync(context, value, noSubList: true);
            return access.Length - 1;
        }

        /// <summary>
        /// The lowest common ancestor
        /// </summary>
        [Schema]
        public static async Task<string> lca(SchemaContext context, [Schema(NS_SYSTEM_SCHEMA_TYPE_ENUM)] string @enum, string[] values)  
        {
            values = values.Select(v => v.Trim()).Where(v => !string.IsNullOrWhiteSpace(v)).ToArray();
            if (values.Length == 0) return string.Empty;
            EnumType? enumType = await context.GetSchemaTypeAsync<EnumType>(@enum);
            if (enumType == null) return string.Empty;
            EnumValueAccess[] access = await enumType.LoadEnumAccessListAsync(context, values[0], noSubList: true);
            for (int i = 1; i < values.Length; i++)
            {
                EnumValueAccess[] next = await enumType.LoadEnumAccessListAsync(context, values[i], noSubList: true);
                if (next.Length == 0) { access = []; break; }
                for (int j = 0; j < access.Length && j < next.Length; j++)
                {
                    if (!access[j].Value.Equals(next[j].Value))
                    {
                        access = access.Take(j).ToArray();
                        break;
                    }
                }
                if (access.Length > next.Length) access = access.Take(next.Length).ToArray();
                if (access.Length == 0) break;
            }
            return access.Length > 0 ? access[access.Length - 1].Value : string.Empty;
        }
    }

    [Schema($"{NS_SYSTEM_DATA}.recognizer")]
    public static class RecognizerOper
    {
        /// <summary>
        /// Validate whether a string matches the recognizer format
        /// </summary>
        [Schema]
        public static async Task<bool> validate(SchemaContext context, [Schema(NS_SYSTEM_SCHEMA_TYPE_RECOGNIZER)] string recognizer, string value)
        {
            if (string.IsNullOrEmpty(value)) return false;

            RecognizerType? recognizerType = await context.GetSchemaTypeAsync<RecognizerType>(recognizer);
            if (recognizerType == null) return false;

            RecognizeOutput result = await recognizerType.RecognizeAsync(context, value);
            return result.Success;
        }

        /// <summary>
        /// Parse a string into a structured value using the recognizer (string → type)
        /// </summary>
        [Schema]
        public static async Task<T?> parse<T>(SchemaContext context, [Schema(NS_SYSTEM_SCHEMA_TYPE_RECOGNIZER)] string recognizer, string value)
        {
            if (string.IsNullOrEmpty(value)) return default;

            RecognizerType? recognizerType = await context.GetSchemaTypeAsync<RecognizerType>(recognizer);
            if (recognizerType == null) return default;

            RecognizeOutput result = await recognizerType.RecognizeAsync(context, value);
            if (!result.Success || result.Value == null) return default;

            return result.Value.ToValue<T>();
        }

        /// <summary>
        /// Convert a structured value to a string using the recognizer (type → string)
        /// </summary>
        [Schema]
        public static async Task<string?> emit<T>(SchemaContext context, [Schema(NS_SYSTEM_SCHEMA_TYPE_RECOGNIZER)] string recognizer, T value)
        {
            if (value == null) return null;

            RecognizerType? recognizerType = await context.GetSchemaTypeAsync<RecognizerType>(recognizer);
            if (recognizerType == null) return null;

            AnySchemaNode? node = value as AnySchemaNode;
            if (node == null)
            {
                // Try to create a node from the source type
                AnySchemaType? sourceType = !string.IsNullOrWhiteSpace(recognizerType.SourceType)
                    ? await context.GetSchemaTypeAsync(recognizerType.SourceType)
                    : null;
                if (sourceType == null) return null;
                node = sourceType.CreateNode(value);
                if (node == null || node.IsEmpty) return null;
            }

            return await recognizerType.EmitAsync(context, node);
        }
    }

    #region Context Item

    /// <summary>
    /// Gets the context item
    /// </summary>
    [Schema]
    public static AnySchemaNode? getcontext(SchemaContext context, string item) 
        => context.GetSchemaContextItem(item);

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

        ArrayType? arrType = fieldType.SchemaType as ArrayType;
        string[] keys = arrType?.Primary ?? [];

        // full primary key contains
        if (keys.Length != args.Length) return default;

        // Check the app access is contains, only allow access in the same app or system depth app
        Access? access = context.GetSchemaContextItem<Access>();
        if ((access == null || !app.Equals(access.App)) && appType!.ScopeType != Enum.AppScopeType.SystemLevel)
            return default;

        // get the key type
        AppSchemaDataFilter? filter = null;
        List<AnySchemaNode>[] keyValues = new List<AnySchemaNode>[keys.Length];
        for (int i = 0; i < keys.Length; i++)
        {
            AnySchemaType? keyType = (arrType!.ElementSchemaType as StructType)?.GetField(keys[i])?.SchemaType;
            AnySchemaNode? valueNode = keyType?.CreateNode(args[i]);
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

        (AnySchemaNode? value, _) = await context.GetAppFieldDataAsync(fieldType, keys.Length == 0 ? AppSchemaDataResult.First : AppSchemaDataResult.List, filter);
        AnySchemaNode? result = value;
        if (keys.Length > 0)
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
                    AnySchemaNode key = keyValues[i][j];
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
        AnySchemaNode? result = await get<AnySchemaNode>(context, app, field, args);
        AnySchemaNode? f = (result as StructNode)?.GetField(dataField);
        return f != null ? f.ToValue<T>() : null;
    }
    
    #endregion

    #region Data Source

    /// <summary>
    /// Generate a data source for the app field, waiting for query, the codes won't be execution unless use it in wrong way
    /// </summary>
    [Schema]
    public static async Task<ArrayNode> getdatasource(
        SchemaContext context,
        [Schema(NS_SYSTEM_SCHEMA_DOMAIN_APP)] string app,
        [Schema(NS_SYSTEM_SCHEMA_DOMAIN_FIELD)] string field)
    {
        AppType? appType = !string.IsNullOrEmpty(app)
            ? await context.GetAppTypeAsync(app)
            : null;

        AppFieldType? fieldType = appType?.GetField(field);
        if (fieldType?.SchemaType == null) throw new InvalidOperationException($"The field {field} not found in the app {app}.");
        if (fieldType.SchemaType is not ArrayType) throw new InvalidOperationException($"The field {field} type is not array type in the app {app}.");
        return new ArrayNode(fieldType.SchemaType);
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
            || fieldType.SchemaType is EnumType 
            || fieldType.SchemaType is ScalarType { IsNumber: false } 
            || fieldType.SchemaType is StructType s && !s.Fields.Any(f => f.SchemaType is ScalarType {  IsNumber: true })
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
                    if (dataNode is not ScalarNode) goto ROLLBACK;
                    origin = fieldType.SchemaType.CreateNode(
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
                        AnySchemaNode? orgFld = originStruct.GetField(fld.Name);
                        AnySchemaNode? dataFld = structData.GetField(fld.Name);

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
                                AnySchemaNode? orgFld = oitem.GetField(fld.Name);
                                AnySchemaNode? dataFld = ditem.GetField(fld.Name);

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
        
        (_, AnySchemaNode? dataNode, JsonNode? error) = await fieldType.ValidateDataAsync(context, data);
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

        (_, AnySchemaNode? dataNode, JsonNode? error) = await fieldType.ValidateDataAsync(context, data);
        if (error != null || dataNode == null || dataNode.IsEmpty) return false;

        using var stack = context.StackAccess(app, target);

        await context.BeginTransactionAsync();
        if (fieldType.SchemaType is ArrayType { Primary: {  Length: > 0 }})
            await context.DeleteFieldListDataAsync(fieldType, dataNode);
        else
            await context.SaveFieldDataAsync(fieldType, null);
        await context.CommitTransactionAsync(!raiseEvent);
        return true;
    }

    #endregion
}