using Microsoft.Extensions.Logging;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Function;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json.Nodes;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.Schema;

namespace SchemaNode.Components;

public static class AppDataTransactionExtension
{
    #region Save

    /// <summary>
    /// Save entity data
    /// </summary>
    public static async Task<bool> SaveEntityAsync<T>(this SchemaContext context, string target, T value)
    {
        (AppFieldType appFieldType, _) = await context.AssertAppField<T>();
        return await SaveFieldDataAsync(context, appFieldType, target, appFieldType.SchemaType!.CreateNode(value));
    }

    /// <summary>
    /// Save entity list data
    /// </summary>
    public static async Task<bool> SaveEntitiesAsync<T>(this SchemaContext context, string target, List<T> values)
    {
        (AppFieldType appFieldType, IReadOnlyList<PropertyInfo>? primarys) = await context.AssertAppField<T>();
        if (primarys == null) throw new ArgumentException($"The app field of {typeof(T).FullName} only support single value");
        return await SaveFieldDataAsync(context, appFieldType, target, appFieldType.SchemaType!.CreateNode(values));
    }

    /// <summary>
    /// Save entity data
    /// </summary>
    public static Task<bool> SaveFieldEntityAsync<T>(this SchemaContext context, AppFieldType field, string target, T value)
    {
        context.AssertType<T>(field);
        return SaveFieldDataAsync(context, field, target, field.SchemaType!.CreateNode(value));
    }

    /// <summary>
    /// Save entity list data
    /// </summary>
    public static Task<bool> SaveFieldEntitiesAsync<T>(this SchemaContext context, AppFieldType field, string target, List<T> values)
    {
        context.AssertType<T>(field);
        return SaveFieldDataAsync(context, field, target, field.SchemaType!.CreateNode(values));
    }

    /// <summary>
    /// Sve field data
    /// </summary>
    public static Task<bool> SaveFieldDataAsync(this SchemaContext context, AppFieldType field, string target, JsonNode? value = null)
    {
        AnySchemaNode data = field.SchemaType!.CreateNode(value) ?? throw new NotSupportedException();
        return SaveFieldDataAsync(context, field, target, data);
    }

    /// <summary>
    /// Save the field data by data
    /// </summary>
    public static async Task<bool> SaveFieldDataAsync(this SchemaContext context, AppFieldType field, string target, AnySchemaNode? value = null, bool innerCall = false, bool canAdd = true, bool onlyAdd = false)
    {
        // no front only & enable & no source ref
        if (!field.EnableDynamicTable) return false;
        if (field.Readonly == true && !innerCall) return false; // readonly can only be set by system

        // Not allow the direct data update
        if (!innerCall && !string.IsNullOrWhiteSpace(field.Func)) return false;
        var dataProvider = context.GetService<IAppDataProvider>() ?? throw new InvalidOperationException(APP_DATA_PROVIDER_NOT_EXIST);

        // Prepare
        DynamicTableSchema schema = await context.PrepareFieldDataAsync(field);

        try
        {
            (bool result, AnySchemaNode? update, AnySchemaNode? origin) = await dataProvider.SaveDynamicTableDataAsync(schema, target, value, canAdd, onlyAdd);
            if (result) OnFieldDataChanged(context, target, field, TransactionChangeOperation.Modify, update, origin);
            return result;
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex.Message);
            throw;
        }
    }

    #endregion

    #region Delete

    /// <summary>
    /// Delete entity data
    /// </summary>
    public static async Task DeleteEntityAsync<T>(this SchemaContext context, string target, T value)
    {
        (AppFieldType appFieldType, IReadOnlyList<PropertyInfo>? primarys) = await context.AssertAppField<T>();

        if (primarys != null)
        {
            JsonObject query = [];
            foreach (PropertyInfo prop in primarys)
            {
                query[prop.Name.ToCamelCase()] = JsonValue.Create(prop.GetValue(value) ?? throw new ArgumentException($"The primary key {prop.Name} value is null"));
            }
            await DeleteFieldListDataAsync(context, appFieldType, target, [query]);
        }
        else
        {
            await SaveFieldDataAsync(context, appFieldType, target, null);
        }
    }

    /// <summary>
    /// Delete entity data
    /// </summary>
    public static async Task DeleteEntityAsync<T>(this SchemaContext context, string target, params object[] keys)
    {
        (AppFieldType appFieldType, IReadOnlyList<PropertyInfo>? primarys) = await context.AssertAppField<T>();
        if (primarys == null) throw new ArgumentException($"The app field of type {typeof(T).FullName} only support single value");
        if (keys.Length != primarys.Count) throw new ArgumentException($"The type {typeof(T).FullName} primary key count not match");

        JsonObject query = [];
        for (int i = 0; i < keys.Length; i++)
            query[primarys[i].Name.ToCamelCase()] = JsonValue.Create(keys[i]);

        await DeleteFieldListDataAsync(context, appFieldType, target, [query]);
    }

    /// <summary>
    /// Delete entity data
    /// </summary>
    public static async Task DeleteEntitiesAsync<T>(this SchemaContext context, string target, List<T> value)
    {
        (AppFieldType appFieldType, IReadOnlyList<PropertyInfo>? primarys) = await context.AssertAppField<T>();
        if (primarys == null) throw new ArgumentException($"The app field of type {typeof(T).FullName} only support single value");

        JsonArray query = [];
        foreach (T valueItem in value)
        {
            JsonObject q = [];
            foreach (PropertyInfo prop in primarys)
            {
                q[prop.Name.ToCamelCase()] = JsonValue.Create(prop.GetValue(valueItem) ?? throw new ArgumentException($"The primary key {prop.Name} value is null"));
            }
            query.Add(q);
        }

        await DeleteFieldListDataAsync(context, appFieldType, target, query);
    }

    /// <summary>
    /// Delete entity data
    /// </summary>
    public static async Task DeleteEntitiesAsync<T>(this SchemaContext context, string target, Expression<Func<T, bool>> cond)
    {
        (AppFieldType appFieldType, IReadOnlyList<PropertyInfo>? primarys) = await context.AssertAppField<T>();
        if (primarys == null) throw new ArgumentException($"The app field of type {typeof(T).FullName} only support single value");

        EntityConditionVisitor vistor = new();
        vistor.Visit(cond);
        JsonNode filter = vistor.Condition;

        if (filter is JsonObject obj && !obj.IsEmpty())
            await DeleteFieldListDataAsync(context, appFieldType, target, [filter]);
        else
            throw new ArgumentException("The conditon is not valid");
    }

    /// <summary>
    /// Delete entity data
    /// </summary>
    public static Task DeleteFieldEntityAsync<T>(this SchemaContext context, AppFieldType field, string target, T value)
    {
        context.AssertType<T>(field);
        return DeleteFieldListDataAsync(context, field, target, [value.ToJsonNode()]);
    }

    /// <summary>
    /// Delete entity data
    /// </summary>
    public static Task DeleteEntitiesAsync<T>(this SchemaContext context, AppFieldType field, string target, List<T> value)
    {
        context.AssertType<T>(field);
        return DeleteFieldListDataAsync(context, field, target, (JsonArray?)value.ToJsonNode() ?? []);
    }

    /// <summary>
    /// Delete entity data
    /// </summary>
    public static async Task DeleteEntitiesAsync<T>(this SchemaContext context, AppFieldType field, string target, Expression<Func<T, bool>> cond)
    {
        context.AssertType<T>(field);

        EntityConditionVisitor vistor = new();
        vistor.Visit(cond);
        JsonNode filter = vistor.Condition;

        if (filter is JsonObject obj && !obj.IsEmpty())
            await DeleteFieldListDataAsync(context, field, target, [filter]);
        else
            throw new ArgumentException("The conditon is not valid");
    }

    /// <summary>
    /// Delete the list from a list-struct type field data
    /// </summary>
    public static async Task DeleteFieldListDataAsync(this SchemaContext context, AppFieldType field, string target, JsonArray query, bool innerCall = false)
    {
        // no front only & enable & no source ref
        if (!field.EnableDynamicTable) return;
        var dataProvider = context.GetService<IAppDataProvider>() ?? throw new InvalidOperationException(APP_DATA_PROVIDER_NOT_EXIST);

        // Prepare
        DynamicTableSchema schema = await context.PrepareFieldDataAsync(field);

        // Only non-single schema can be used
        if (schema.Single) return;
        try
        {
            if (query.Count == 0) return;
            (bool result, AnySchemaNode? origin) = await dataProvider.DeleteDynamicTableDataAsync(schema, target, query);
            if (result) OnFieldDataChanged(context, target, field, TransactionChangeOperation.Delete, null, origin);
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Delete the target's field data
    /// </summary>
    public static async Task DeleteFieldDataAsync(this SchemaContext context, AppFieldType field, string target, bool innerCall = false)
    {
        // no front only & enable & no source ref
        if (!field.EnableDynamicTable) return;
        var dataProvider = context.GetService<IAppDataProvider>() ?? throw new InvalidOperationException(APP_DATA_PROVIDER_NOT_EXIST);

        // Prepare
        DynamicTableSchema schema = await context.PrepareFieldDataAsync(field);

        try
        {
            (bool result, AnySchemaNode? origin) = await dataProvider.DeleteDynamicTableDataAsync(schema, target);
            if (result)
                OnFieldDataChanged(context, target, field,
                    schema.Single ? TransactionChangeOperation.Delete : TransactionChangeOperation.DropAll,
                    null, origin);
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex.Message);
            throw;
        }
    }

    #endregion

    #region Transaction & Data Push

    /// <summary>
    /// Begin transaction.
    /// </summary>
    public static async Task BeginTransactionAsync(this SchemaContext context)
    {
        var dataProvider = context.GetService<IAppDataProvider>() ?? throw new InvalidOperationException(APP_DATA_PROVIDER_NOT_EXIST);
        await dataProvider.BeginTransactionAsync();
        context.SetContextItem(new Dictionary<string, TransactionChangeData>()); // keep track
    }

    /// <summary>
    /// Commit transaction.
    /// </summary>
    public static async Task CommitTransactionAsync(this SchemaContext context, bool pushAll = false, bool pushAllFields = false)
    {
        var dataProvider = context.GetService<IAppDataProvider>() ?? throw new InvalidOperationException(APP_DATA_PROVIDER_NOT_EXIST);
        var transChangedData = context.GetOrCreateContextItem<Dictionary<string, TransactionChangeData>>();

        // Process data field push
        foreach (string target in transChangedData.Keys.ToArray())
            await ProcessDataPush(context, target, transChangedData[target], pushAll, pushAllFields);

        // Commit
        await dataProvider.CommitTransactionAsync();

        // Event after commit
        foreach (var (target, value) in transChangedData)
        {
            foreach (var (field, changes) in value.Changes)
            {
                foreach (var change in changes)
                {
                    switch (change.Operation)
                    {
                        case TransactionChangeOperation.Create:
                            {
                                // Raise create event
                                AnySchemaNode? newValue = change.Value;
                                if (newValue is ArrayTypeNode arr && field.SchemaType is ArrayType { Primary.Length: > 0 })
                                {
                                    foreach (AnySchemaNode item in arr)
                                        context.RaiseEvent(new AppFieldDataCreateEvent(field, target), item);
                                }
                                else if (newValue != null)
                                {
                                    context.RaiseEvent(new AppFieldDataCreateEvent(field, target), newValue);
                                }

                                break;
                            }
                        case TransactionChangeOperation.Modify:
                            {
                                AnySchemaNode? changeValues = change.Value;
                                AnySchemaNode? originValues = change.Origin;
                                if (changeValues is ArrayTypeNode arr && field.SchemaType is ArrayType { Primary.Length: > 0 } type)
                                {
                                    Dictionary<string, AnySchemaNode> originMap = [];
                                    if (originValues is ArrayTypeNode oldArr)
                                    {
                                        foreach (AnySchemaNode node in oldArr)
                                        {
                                            if (node is not StructTypeNode structNode) continue;
                                            string? key = type.GetPrimaryKey(structNode);
                                            if (string.IsNullOrEmpty(key)) continue;
                                            originMap[key] = structNode;
                                        }
                                    }

                                    // Raise update event or create event
                                    foreach (AnySchemaNode node in arr)
                                    {
                                        if (node is StructTypeNode structNode)
                                        {
                                            string? key = type.GetPrimaryKey(structNode);
                                            if (string.IsNullOrEmpty(key)) continue;

                                            if (originMap.Remove(key, out AnySchemaNode? o))
                                            {
                                                structNode.Origin = o;
                                                context.RaiseEvent(new AppFieldDataUpdateEvent(field, target), structNode);
                                            }
                                            else
                                            {
                                                context.RaiseEvent(new AppFieldDataCreateEvent(field, target), structNode);
                                            }
                                        }
                                    }

                                    // Raise delete event for remaining origin
                                    foreach (AnySchemaNode node in originMap.Values)
                                    {
                                        context.RaiseEvent(new AppFieldDataDeleteEvent(field, target), node);
                                    }
                                }
                                else if (changeValues != null)
                                {
                                    changeValues.Origin = originValues;
                                    if (originValues == null)
                                        context.RaiseEvent(new AppFieldDataCreateEvent(field, target), changeValues);
                                    else
                                        context.RaiseEvent(new AppFieldDataUpdateEvent(field, target), changeValues);
                                }
                                break;
                            }
                        case TransactionChangeOperation.Delete:
                        case TransactionChangeOperation.DropAll:
                            {
                                AnySchemaNode? origin = change.Origin;
                                if (origin is ArrayTypeNode arr && field.SchemaType is ArrayType { Primary.Length: > 0 })
                                {
                                    foreach (AnySchemaNode item in arr)
                                        context.RaiseEvent(new AppFieldDataDeleteEvent(field, target), item);
                                }
                                else if (origin != null)
                                {
                                    context.RaiseEvent(new AppFieldDataDeleteEvent(field, target), origin);
                                }
                                break;
                            }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Rollback transaction.
    /// </summary>
    public static async Task RollbackTransactionAsync(this SchemaContext context)
    {
        var dataProvider = context.GetService<IAppDataProvider>() ?? throw new InvalidOperationException(APP_DATA_PROVIDER_NOT_EXIST);
        await dataProvider.RollbackTransactionAsync();
        context.SetContextItem<Dictionary<string, TransactionChangeData>>(null);
    }

    // Process the data push
    static async Task ProcessDataPush(SchemaContext context, string target, TransactionChangeData changeData, bool pushAll = false, bool pushAllFields = false, AppFieldType? pushNode = null)
    {
        #region Generate push orders

        List<AppFieldType> baseFields = changeData.Changes.Keys.Where(p => p.HasObserver).ToList();

        // If push all
        if (pushAllFields)
        {
            baseFields.Clear();
            foreach (string app in changeData.Changes.Keys.Select(p => p.App).Distinct())
            {
                AppType? appNode = await context.GetAppTypeAsync(app);
                if (appNode?.Fields != null)
                {
                    // Add all the base fields with observers and no function
                    baseFields.AddRange(appNode.Fields.Where(f => f.FuncNode == null && f.HasObserver));
                }
            }
        }

        // Generate the push levels
        FieldDataPushLevel? root = null;
        FieldDataPushLevel? curr = null;
        Dictionary<AppFieldType, FieldDataPushLevel> updateFieldsLvlMap = new();

        // The given push node
        if (pushNode != null)
        {
            root = new FieldDataPushLevel
            {
                Fields =
                {
                    pushNode
                }
            };
            curr = root;
            if (baseFields.Count == 0 && pushNode.HasObserver)
                baseFields = root.Fields;
        }

        // Process levels
        while (baseFields.Count > 0)
        {
            FieldDataPushLevel next = new();

            // Check fields
            foreach (AppFieldType node in baseFields.Where(p => p.HasObserver)
                         .SelectMany(p => p.Observers!).Distinct()
                         .Where(n => !(n.Disable ?? false) && !(n.Frontend ?? false)))
            {
                if (!updateFieldsLvlMap.TryGetValue(node, out FieldDataPushLevel? value))
                {
                    next.Fields.Add(node);
                    updateFieldsLvlMap.Add(node, next);
                }
                else
                {
                    // Move the field to current
                    next.Fields.Add(node);
                    value.Fields.Remove(node);
                    updateFieldsLvlMap[node] = next;
                }
            }

            // Link the levels
            if (next.Fields.Count > 0)
            {
                if (curr != null)
                {
                    curr.Next = next;
                }
                else
                {
                    root = next;
                }
                curr = next;
            }
            else
            {
                break;
            }
            baseFields = next.Fields.Where(p => p.HasObserver).ToList();
        }

        #endregion

        // Process data push
        Dictionary<AppFieldType, AnySchemaNode> otherFields = new();
        HashSet<AppFieldType> displayOnlyGens = [];
        HashSet<string> otherTargets = [];
        while (root?.Fields.Count is > 0)
        {
            foreach (AppFieldType field in root.Fields)
            {
                // Check ref
                AppFieldType? tarField = field;
                string realTarget = target;
                bool notRefField = field.SourceAppType == null || field.TrackPush == true;

                // push to source directly
                if (!notRefField)
                {
                    (tarField, realTarget) = await context.GetSourceFieldNode(field, target, true);
                    if (tarField == null) continue;
                    if (realTarget != target) otherTargets.Add(realTarget);
                }

                // Prepare arguments
                FunctionType? funcNode = field.FuncNode;
                if (funcNode == null || field.FuncArgs == null) continue;
                var args = new FieldDataPushArg[field.FuncArgs.Count];
                int arrayIndex = -1;
                for (int i = 0; i < field.FuncArgs.Count; i++)
                {
                    AppFieldNodeArgument call = field.FuncArgs[i];
                    args[i] = new FieldDataPushArg();

                    // Generate argument
                    List<FieldDataChangeData>? changes = !(pushAll && notRefField) && changeData.Changes.TryGetValue(call.AppField, out List<FieldDataChangeData>? dataChange) ? dataChange : null;
                    args[i].Type = call.AppField.SchemaType!;
                    if (args[i].Type is ArrayType && (funcNode.Args[i].SchemaType is not ArrayType || arrayIndex < 0)) arrayIndex = i;

                    // Check changes
                    if (changes == null)
                    {
                        args[i].IsFull = true;
                        args[i].Changed = false;

                        // full data
                        if (otherFields.ContainsKey(call.AppField))
                        {
                            args[i].Value = otherFields[call.AppField].IsEmpty ? null : otherFields[call.AppField];
                        }
                        else
                        {
                            (args[i].Value, _) = await context.GetFieldDataAsync(call.AppField, target);
                            otherFields[call.AppField] = args[i].Value ?? call.AppField.SchemaType!.CreateNode()!;
                        }
                        args[i].Origin = args[i].Value;
                    }
                    else
                    {
                        // generate display only fields for upload data
                        if (displayOnlyGens.Add(call.AppField))
                        {
                            // check schema
                            if (call.AppField.SchemaType is ArrayType { ElementSchemaType: StructType } or StructType)
                            {
                                DynamicTableSchema schema = await context.PrepareFieldDataAsync(call.AppField);
                                foreach (FieldDataChangeData change in changes)
                                {
                                    // for new
                                    await schema.GenerateDisplayOnlyFields(context, change.Value);

                                    // for origin
                                    await schema.GenerateDisplayOnlyFields(context, change.Origin);
                                }
                            }
                        }

                        args[i].Changed = true;
                        if (call.AppField.SchemaType is ArrayType @array)
                        {
                            // if full data
                            args[i].IsFull = @array.Primary == null || @array.Primary.Length == 0;

                            ArrayTypeNode values = new(@array);
                            ArrayTypeNode origins = new(@array);
                            foreach (FieldDataChangeData change in changes)
                            {
                                switch (change.Operation)
                                {
                                    case TransactionChangeOperation.Create:
                                        if (change.Value is { IsEmpty: false })
                                        {
                                            if (change.Value is ArrayTypeNode vArr)
                                            {
                                                values.AddRange(vArr);
                                            }
                                            else
                                            {
                                                values.Add(change.Value);
                                            }
                                        }
                                        break;
                                    case TransactionChangeOperation.Modify:
                                        if (change.Value is { IsEmpty: false })
                                        {
                                            if (change.Value is ArrayTypeNode vArr)
                                            {
                                                values.AddRange(vArr);
                                            }
                                            else
                                            {
                                                values.Add(change.Value);
                                            }
                                        }
                                        if (change.Origin is { IsEmpty: false })
                                        {
                                            if (change.Origin is ArrayTypeNode vArr)
                                            {
                                                origins.AddRange(vArr);
                                            }
                                            else
                                            {
                                                origins.Add(change.Origin);
                                            }
                                        }
                                        break;
                                    case TransactionChangeOperation.Delete:
                                        if (change.Origin is { IsEmpty: false })
                                        {
                                            if (change.Origin is ArrayTypeNode vArr)
                                            {
                                                origins.AddRange(vArr);
                                            }
                                            else
                                            {
                                                origins.Add(change.Origin);
                                            }
                                        }
                                        break;
                                    case TransactionChangeOperation.DropAll:
                                        args[i].IsFull = true;
                                        if (change.Origin is ArrayTypeNode arr)
                                            origins.AddRange(arr);
                                        break;
                                    default:
                                        throw new ArgumentOutOfRangeException();
                                }
                            }
                            args[i].Value = values;
                            args[i].Origin = origins;
                        }
                        else
                        {
                            args[i].IsFull = true;
                            foreach (FieldDataChangeData change in changes)
                            {
                                switch (change.Operation)
                                {
                                    case TransactionChangeOperation.Create:
                                        args[i].Value = change.Value;
                                        break;
                                    case TransactionChangeOperation.Modify:
                                        args[i].Value = change.Value;
                                        args[i].Origin = change.Origin;
                                        break;
                                    case TransactionChangeOperation.Delete:
                                        args[i].Origin = change.Origin;
                                        break;
                                    case TransactionChangeOperation.DropAll:
                                        args[i].Origin = change.Origin;
                                        break;
                                    default:
                                        throw new ArgumentOutOfRangeException();
                                }
                            }
                        }
                    }

                    // map data field
                    if (!string.IsNullOrWhiteSpace(call.DataField))
                    {
                        if (args[i].Type is StructType)
                        {
                            // Gets the value
                            args[i].Value = ((StructTypeNode?)args[i].Value)?.GetValueByPaths(call.DataField);

                            // Gets the origin
                            args[i].Origin = ((StructTypeNode?)args[i].Origin)?.GetValueByPaths(call.DataField);
                        }
                        else if (args[i].Type is ArrayType { ElementSchemaType: StructType })
                        {
                            // Gets the value
                            if (args[i].Value is ArrayTypeNode arr)
                            {
                                args[i].Value = arr.GetValueByPaths(call.DataField);
                            }

                            // Gets the origin
                            if (args[i].Origin is ArrayTypeNode oArr)
                            {
                                args[i].Origin = oArr.GetValueByPaths(call.DataField);
                            }
                        }
                    }
                }

                // Check if there are changed field beyond part update field, need full update
                // So normally simple field contains the settings that won't be upgraded, but if changes all should be rebuilt
                if (notRefField && args.Any(p => p is { Changed: true, IsArray: false }) && arrayIndex >= 0 && !args[arrayIndex].IsFull)
                {
                    FieldDataPushArg arg = args[arrayIndex];
                    AppFieldNodeArgument call = field.FuncArgs[arrayIndex];

                    // full data
                    if (otherFields.ContainsKey(call.AppField))
                    {
                        arg.Value = otherFields[call.AppField].IsEmpty ? null : otherFields[call.AppField];
                    }
                    else
                    {
                        (arg.Value, _) = await context.GetFieldDataAsync(call.AppField, target);
                        otherFields[call.AppField] = arg.Value ?? call.AppField.SchemaType!.CreateNode()!;
                    }
                    arg.Origin = arg.Value;
                    arg.IsFull = true;
                }

                // If part update or is ref, must get the original calc result
                AnySchemaNode? oldResult = null;
                if (arrayIndex >= 0 && (!args[arrayIndex].IsFull || !notRefField))
                {
                    JsonArray originCall = new();
                    foreach (FieldDataPushArg arg in args)
                        originCall.Add(arg.Origin?.ToJson());

                    // Check use element
                    if (funcNode.Args[arrayIndex].SchemaType is not ArrayType)
                    {
                        oldResult = new ArrayTypeNode(field.SchemaType!);
                        if (args[arrayIndex].Origin is ArrayTypeNode origin)
                        {
                            foreach (AnySchemaNode t in origin)
                            {
                                originCall[arrayIndex] = t.ToJson();
                                JsonNode? calcRes = await context.CallFunctionAsync(field.FuncNode!, originCall);
                                if (calcRes is JsonArray arr)
                                {
                                    ((ArrayTypeNode)oldResult).AddRange(arr.Where(o => !o.IsEmpty()));
                                }
                                else if (!calcRes.IsEmpty())
                                {
                                    ((ArrayTypeNode)oldResult).Add(calcRes!);
                                }
                            }
                        }
                    }
                    else
                    {
                        JsonNode? r = await context.CallFunctionAsync(field.FuncNode!, originCall);
                        oldResult = r is JsonArray arr ? new ArrayTypeNode(field.SchemaType!, arr) : field.SchemaType!.CreateNode(r);
                    }
                }

                // Calc the new result
                AnySchemaNode? newResult;
                JsonArray callArgs = [];
                foreach (FieldDataPushArg arg in args) callArgs.Add(arg.Value?.ToJson());

                // Check use element
                if (arrayIndex >= 0 && funcNode.Args[arrayIndex].SchemaType is not ArrayType)
                {
                    newResult = new ArrayTypeNode(field.SchemaType!);
                    if (args[arrayIndex].Value is ArrayTypeNode origin)
                    {
                        foreach (AnySchemaNode t in origin)
                        {
                            callArgs[arrayIndex] = t.ToJson();
                            JsonNode? calcRes = await context.CallFunctionAsync(field.FuncNode!, callArgs);
                            if (calcRes is JsonArray arr)
                            {
                                ((ArrayTypeNode)newResult).AddRange(arr.Where(e => !e.IsEmpty()));
                            }
                            else if (!calcRes.IsEmpty())
                            {
                                ((ArrayTypeNode)newResult).Add(calcRes!);
                            }
                        }
                    }

                }
                else
                {
                    JsonNode? r = await context.CallFunctionAsync(field.FuncNode!, callArgs);
                    newResult = r is JsonArray arr ? new ArrayTypeNode(field.SchemaType!, arr) : field.SchemaType!.CreateNode(r);
                }

                // Save the incremental data
                await context.SaveIncrementalData(tarField, realTarget, newResult, oldResult);

                // Check if track push field
                if (tarField.EnablePushTrackTable)
                {
                    // push to the real target
                    (AppFieldType? refField, string refTarget) = await context.GetSourceFieldNode(tarField, realTarget, true);
                    if (refField == null || refField == tarField) continue;
                    if (refTarget != target) otherTargets.Add(refTarget);
                    await context.SaveIncrementalData(refField, refTarget, newResult, oldResult);
                }
            }

            // Process next level
            root = root.Next;
        }

        // Process other targets
        foreach (string tar in otherTargets)
        {
            var transChangedData = context.GetOrCreateContextItem<Dictionary<string, TransactionChangeData>>();
            if (transChangedData.TryGetValue(tar, out TransactionChangeData? val))
                await ProcessDataPush(context, tar, val);
        }
    }

    /// <summary>
    /// Save the incremental data
    /// </summary>
    internal static async Task SaveIncrementalData(this SchemaContext context, AppFieldType field, string target, AnySchemaNode? newResult, AnySchemaNode? oldResult)
    {
        // Join the result
        AnySchemaNode? result = null;
        switch (field.SchemaType)
        {
            case EnumType:
                {
                    DataCombineType method = field.Combine ?? DataCombineType.Assign;
                    (AnySchemaNode? origin, _) = await context.GetFieldDataAsync(field, target);
                    AnySchemaNode? now = GroupJoin(newResult, method);

                    // Update with join method
                    switch (method)
                    {
                        case DataCombineType.Assign:
                            {
                                result = now is { IsEmpty: false } ? now : origin;
                                break;
                            }
                        case DataCombineType.Init:
                            {
                                result = origin is { IsEmpty: false } ? origin : now;
                                break;
                            }
                    }
                    break;
                }
            case ScalarType scalar:
                {
                    // Gets the join method
                    DataCombineType method = field.Combine ?? (scalar.IsNumber ? DataCombineType.Sum : DataCombineType.Assign);

                    // Part
                    (AnySchemaNode? origin, _) = await context.GetFieldDataAsync(field, target);
                    AnySchemaNode? old = GroupJoin(scalar, oldResult, method);
                    AnySchemaNode? now = GroupJoin(scalar, newResult, method);

                    // Update with join method
                    switch (method)
                    {
                        case DataCombineType.Assign:
                            {
                                result = now;
                                break;
                            }
                        case DataCombineType.Init:
                            {
                                result = origin is { IsEmpty: false } ? origin : now;
                                break;
                            }
                        case DataCombineType.Sum:
                        case DataCombineType.Count:
                            {
                                result = field.SchemaType.CreateNode(
                                    (origin is { IsEmpty: false } ? origin.ToValue<decimal>() : 0m) +
                                    (now is { IsEmpty: false } ? now.ToValue<decimal>() : 0m) -
                                    (old is { IsEmpty: false } ? old.ToValue<decimal>() : 0m)
                                );
                            }
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                    break;
                }
            case StructType { Fields.Length: > 0 } @struct:
                {
                    // Gets the join method map
                    Dictionary<string, DataCombineType> joinMethodMap = new();

                    // Default join
                    foreach (StructFieldConfig f in @struct.Fields)
                    {
                        if (f.TypeNode is ScalarType s)
                            joinMethodMap[f.Name] = field.Combines?.FirstOrDefault(o => o.Field.Equals(f.Name, StringComparison.OrdinalIgnoreCase))?.Type
                                ?? (s.IsNumber ? DataCombineType.Sum : DataCombineType.Assign);
                    }

                    // Gets the result
                    (AnySchemaNode? origin, _) = await context.GetFieldDataAsync(field, target);
                    AnySchemaNode? old = GroupJoin(@struct, oldResult, joinMethodMap);
                    AnySchemaNode? now = GroupJoin(@struct, newResult, joinMethodMap);

                    // Update with join method
                    if ((origin == null || origin.IsEmpty) && (old == null || old.IsEmpty))
                    {
                        result = now;
                    }
                    else
                    {
                        StructTypeNode final = new StructTypeNode(@struct);
                        foreach (StructFieldConfig nodeField in @struct.Fields)
                        {
                            AnySchemaNode? originFld = origin is StructTypeNode os ? os.GetField(nodeField.Name) : null;
                            AnySchemaNode? oldFld = old is StructTypeNode ols ? ols.GetField(nodeField.Name) : null;
                            AnySchemaNode? nowFld = now is StructTypeNode ns ? ns.GetField(nodeField.Name) : null;

                            switch (joinMethodMap.GetValueOrDefault(nodeField.Name, DataCombineType.Assign))
                            {
                                case DataCombineType.Assign:
                                    {
                                        final[field.Name] = nowFld is { IsEmpty: false } ? nowFld : originFld;
                                        break;
                                    }
                                case DataCombineType.Init:
                                    {
                                        final[nodeField.Name] = originFld is { IsEmpty: false } ? originFld : nowFld;
                                        break;
                                    }
                                case DataCombineType.Sum when nodeField.TypeNode is ScalarType { IsNumber: true }:
                                case DataCombineType.Count when nodeField.TypeNode is ScalarType { IsNumber: true }:
                                    {
                                        final[nodeField.Name] = nodeField.TypeNode.CreateNode(
                                            (originFld is { IsEmpty: false } ? originFld.ToValue<decimal>() : 0m) +
                                            (nowFld is { IsEmpty: false } ? nowFld.ToValue<decimal>() : 0m) -
                                            (oldFld is { IsEmpty: false } ? oldFld.ToValue<decimal>() : 0m)
                                        );
                                        break;
                                    }
                                default:
                                    throw new ArgumentOutOfRangeException();
                            }
                        }
                        result = final;
                    }

                    break;
                }
            case ArrayType { ElementSchemaType: EnumType or ScalarType }:
                {
                    result = newResult;
                    break;
                }
            case ArrayType { ElementSchemaType: StructType { Fields: { Length: > 0 } } structNode, Primary: { Length: > 0 } } array:
                {
                    // Gets the join method map
                    Dictionary<string, DataCombineType> joinMethodMap = new();

                    // Gets the value fields
                    List<string> valueFields = new();
                    Dictionary<string, AnySchemeType> primaryNodes = new();
                    foreach (StructFieldConfig fieldType in structNode.Fields)
                    {
                        if (!array.Primary.Contains(fieldType.Name))
                        {
                            valueFields.Add(fieldType.Name);

                            if (fieldType.TypeNode is ScalarType s)
                            {
                                joinMethodMap[fieldType.Name] = s.IsNumber ? DataCombineType.Sum : DataCombineType.Assign;
                            }
                        }
                        else
                            primaryNodes.Add(fieldType.Name, fieldType.TypeNode!);
                    }

                    // Based on array join methods
                    if (array.Combines != null)
                    {
                        foreach (DataCombine combine in array.Combines)
                        {
                            joinMethodMap[combine.Field] = combine.Type;
                        }
                    }
                    // Based on field join methods
                    if (field.Combines != null)
                    {
                        foreach (DataCombine combine in field.Combines)
                        {
                            joinMethodMap[combine.Field] = combine.Type;
                        }
                    }

                    // Generate result map
                    
                    // filter old & new result with primary keys
                    oldResult = oldResult is ArrayTypeNode oldArr ? oldArr.FilterByPrimaryKeys(array.Primary) : oldResult;
                    newResult = newResult is ArrayTypeNode newArr ? newArr.FilterByPrimaryKeys(array.Primary) : newResult;
                    
                    // Group join the old & now data
                    Dictionary<string, StructTypeNode> oldMap = GroupJoinObjectMap(array, oldResult, joinMethodMap);
                    Dictionary<string, StructTypeNode> nowMap = GroupJoinObjectMap(array, newResult, joinMethodMap);

                    // Query the original data
                    HashSet<string> keys = new();
                    JsonArray query = new();
                    foreach ((string key, StructTypeNode obj) in oldMap)
                    {
                        if (!keys.Add(key)) continue;
                        query.Add(obj.ToJson());
                    }
                    foreach ((string key, StructTypeNode obj) in nowMap)
                    {
                        if (!keys.Add(key)) continue;
                        query.Add(obj.ToJson());
                    }

                    // Gets the original data
                    Dictionary<string, StructTypeNode> resultMap = new Dictionary<string, StructTypeNode>();
                    if (!query.IsEmpty())
                    {
                        (AnySchemaNode? value, _) = await context.GetFieldDataAsync(field, target, query);
                        if (value is ArrayTypeNode arr)
                        {
                            foreach (AnySchemaNode token in arr)
                            {
                                if (token is not StructTypeNode obj) continue;
                                string? key = array.GetPrimaryKey(obj);
                                if (string.IsNullOrWhiteSpace(key)) continue;
                                resultMap[key] = obj;
                            }
                        }
                    }

                    // Generate the result map
                    foreach (string key in keys)
                    {
                        if (resultMap.TryGetValue(key, out var res1))
                        {
                            oldMap.TryGetValue(key, out StructTypeNode? old);
                            nowMap.TryGetValue(key, out StructTypeNode? now);
                            foreach (string s in valueFields)
                            {
                                AnySchemaNode? originFld = res1.GetField(s);
                                AnySchemaNode? oldFld = old?.GetField(s);
                                AnySchemaNode? nowFld = now?.GetField(s);

                                switch (joinMethodMap.GetValueOrDefault(s, DataCombineType.Assign))
                                {
                                    case DataCombineType.Assign:
                                        if (nowFld is { IsEmpty: false })
                                            res1[s] = nowFld;
                                        break;
                                    case DataCombineType.Init:
                                        if (originFld == null || originFld.IsEmpty)
                                            res1[s] = nowFld;
                                        break;
                                    case DataCombineType.Sum:
                                    case DataCombineType.Count:
                                        res1[s] = (originFld is { IsEmpty: false } ? originFld.ToValue<decimal>() : 0m) +
                                            (nowFld is { IsEmpty: false } ? nowFld.ToValue<decimal>() : 0m) -
                                            (oldFld is { IsEmpty: false } ? oldFld.ToValue<decimal>() : 0m);
                                        break;
                                    default:
                                        throw new ArgumentOutOfRangeException();
                                }
                            }
                        }
                        else if (nowMap.TryGetValue(key, out StructTypeNode? res))
                        {
                            resultMap.Add(key, res);
                            if (!oldMap.TryGetValue(key, out StructTypeNode? old)) continue;

                            // Shouldn't be but still handle it
                            foreach (string s in valueFields)
                            {
                                AnySchemaNode? oldFld = old?.GetField(s);
                                AnySchemaNode? nowFld = res?.GetField(s);

                                switch (joinMethodMap.GetValueOrDefault(s, DataCombineType.Assign))
                                {
                                    case DataCombineType.Assign:
                                        if (nowFld == null || nowFld.IsEmpty)
                                            res![s] = oldFld;
                                        break;
                                    case DataCombineType.Init:
                                        if (oldFld is { IsEmpty: false })
                                            res![s] = oldFld;
                                        break;
                                    case DataCombineType.Sum:
                                    case DataCombineType.Count:
                                        res![s] = (nowFld is { IsEmpty: false } ? nowFld.ToValue<decimal>() : 0m) -
                                            (oldFld is { IsEmpty: false } ? oldFld.ToValue<decimal>() : 0m);
                                        break;
                                    default:
                                        throw new ArgumentOutOfRangeException();
                                }
                            }
                        }
                    }

                    // Convert the map to list, sorted by primary keys
                    List<StructTypeNode> joinObjs = resultMap.Values.ToList();
                    joinObjs.Sort((a, b) =>
                    {
                        foreach (string s in array.Primary)
                        {
                            switch (primaryNodes[s])
                            {
                                case ScalarType { IsDate: true }:
                                    {
                                        DateTime ad = a.GetField(s)!.ToValue<DateTime>();
                                        DateTime bd = b.GetField(s)!.ToValue<DateTime>();
                                        if (!SystemDate.notequal(ad, bd))
                                            return SystemDate.lessthan(ad, bd) ? -1 : 1;
                                        break;
                                    }
                                case ScalarType { IsNumber: true }:
                                    {
                                        decimal ad = a.GetField(s)!.ToValue<decimal>();
                                        decimal bd = b.GetField(s)!.ToValue<decimal>();
                                        if (ad != bd)
                                            return ad < bd ? -1 : 1;
                                        break;
                                    }
                                default:
                                    {
                                        string ad = a[s]?.ToString() ?? "";
                                        string bd = b[s]?.ToString() ?? "";
                                        if (!ad.Equals(bd))
                                            return string.Compare(ad, bd, StringComparison.OrdinalIgnoreCase);
                                        break;
                                    }
                            }
                        }
                        return 0;
                    });

                    // Save to result
                    result = field.SchemaType.CreateNode(joinObjs);
                    break;
                }
        }

        // Save
        await SaveFieldDataAsync(context, field, target, result, true);
    }

    // Record the changed fields with changed values
    static void OnFieldDataChanged(SchemaContext context, string target, AppFieldType field, TransactionChangeOperation operation, AnySchemaNode? value = null, AnySchemaNode? origin = null)
    {
        var transChangedData = context.GetOrCreateContextItem<Dictionary<string, TransactionChangeData>>();
        if (!transChangedData.TryGetValue(target, out TransactionChangeData? changeData))
        {
            changeData = new TransactionChangeData();
            transChangedData.Add(target, changeData);
        }
        if (changeData.Changes.TryGetValue(field, out List<FieldDataChangeData>? changes))
        {
            changes.Add(new FieldDataChangeData(operation, value, origin));
        }
        else
        {
            changeData.Changes.Add(field, [new FieldDataChangeData(operation, value, origin)]);
        }
    }

    #endregion
}
