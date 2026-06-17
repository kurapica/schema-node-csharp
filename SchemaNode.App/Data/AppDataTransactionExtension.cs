using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using System.Text.Json.Nodes;
using static SchemaNode.Utility.AppConstant;
using SchemaNode.Utility;
using SchemaNode.Event;
using AppType = SchemaNode.Runtime.AppType;
using ArrayType = SchemaNode.Runtime.ArrayType;
using DateType = SchemaNode.Runtime.DateType;
using DecimalType = SchemaNode.Runtime.DecimalType;
using EnumType = SchemaNode.Runtime.EnumType;
using IntType = SchemaNode.Runtime.IntType;
using StructType = SchemaNode.Runtime.StructType;

// ReSharper disable InconsistentNaming

namespace SchemaNode.Data;

public static class AppDataTransactionExtension
{
    #region Save

    /// <summary>
    /// Save field data
    /// </summary>
    public static Task<bool> SaveFieldDataAsync(this SchemaContext context, AppFieldType field, JsonNode? value = null)
        => context.SaveFieldDataAsync(field, field.ValueType!.From(value) ?? throw new NotSupportedException());

    /// <summary>
    /// Save the field data by data
    /// </summary>
    public static async Task<bool> SaveFieldDataAsync(this SchemaContext context, AppFieldType field, DataNode? value = null, bool innerCall = false, bool canAdd = true, bool onlyAdd = false, string[]? overrides = null)
    {
        // no front only & enable & no source ref
        if (!field.EnableDynamicTable || field.IsForeignView) return false;
        if (field.PushSource != null && !innerCall) return false; // push field can't be update directly

        var dataProvider = context.GetService<IAppDataProvider>() ?? throw new InvalidOperationException(AppErrorCodes.APP_DATA_PROVIDER_NOT_EXIST);

        // Prepare
        DynamicTableSchema schema = await context.PrepareFieldDataAsync(field);

        try
        {
            (bool result, DataNode? update, DataNode? origin) = await dataProvider.SaveDynamicTableDataAsync(schema, value, canAdd, onlyAdd, overrides);
            if (result) OnFieldDataChanged(context, field, TransactionChangeOperation.Modify, update, origin);
            return result;
        }
        catch (Exception ex)
        {
            context.LogError(ex.Message);
            throw;
        }
    }

    public static async Task<bool> ClearFieldDataAsync(this SchemaContext context, AppFieldType field, bool innerCall = false)
    {
        // no front only & enable & no source ref
        if (!field.EnableDynamicTable || field.IsForeignView) return false;
        if (field.PushSource != null && !innerCall) return false; // push field can't be update directly

        var dataProvider = context.GetService<IAppDataProvider>() ?? throw new InvalidOperationException(AppErrorCodes.APP_DATA_PROVIDER_NOT_EXIST);

        // Prepare
        DynamicTableSchema schema = await context.PrepareFieldDataAsync(field);

        try
        {
            (bool result, DataNode? origin) = await dataProvider.ClearDynamicTableDataAsync(schema);
            if (result) OnFieldDataChanged(context, field, TransactionChangeOperation.DropAll, null, origin);
            return result;
        }
        catch (Exception ex)
        {
            context.LogError(ex.Message);
            throw;
        }
    }

    #endregion

    #region Delete

    /// <summary>
    /// Delete the list from a list-struct type field data
    /// </summary>
    public static async Task<bool> DeleteFieldListDataAsync(this SchemaContext context, AppFieldType field, DataNode nodes)
    {
        // no front only & enable & no source ref
        if (!field.EnableDynamicTable || field.IsForeignView) return false;
        var dataProvider = context.GetService<IAppDataProvider>() ?? throw new InvalidOperationException(AppErrorCodes.APP_DATA_PROVIDER_NOT_EXIST);

        // Prepare
        DynamicTableSchema schema = await context.PrepareFieldDataAsync(field);

        // Only non-single schema can be used
        if (schema.Single) return false;
        try
        {
            if (nodes is ArrayNode { Count: 0 }) return false; // pass if no node to delete
            (bool result, DataNode? origin) = await dataProvider.DeleteSchemaNodeAsync(schema, nodes);
            if (result) OnFieldDataChanged(context, field, TransactionChangeOperation.Delete, null, origin);
        }
        catch (Exception ex)
        {
            context.LogError(ex.Message);
            throw;
        }

        return true;
    }

    /// <summary>
    /// Delete the list from a list-struct type field data by filter
    /// </summary>
    public static async Task<bool> DeleteFieldListDataAsync(this SchemaContext context, AppFieldType field, AppSchemaDataFilter filter)
    {
        // no front only & enable & no source ref
        if (!field.EnableDynamicTable || field.IsForeignView) return false;
        var dataProvider = context.GetService<IAppDataProvider>() ?? throw new InvalidOperationException(AppErrorCodes.APP_DATA_PROVIDER_NOT_EXIST);

        // Prepare
        DynamicTableSchema schema = await context.PrepareFieldDataAsync(field);

        // Only non-single schema can be used
        if (schema.Single) return false;
        try
        {
            (bool result, DataNode? origin) = await dataProvider.DeleteDynamicTableDataAsync(schema, filter);
            if (result) OnFieldDataChanged(context, field, TransactionChangeOperation.Delete, null, origin);
        }
        catch (Exception ex)
        {
            context.LogError(ex.Message);
            throw;
        }

        return true;
    }
    
    #endregion

    #region Transaction & Data Push

    /// <summary>
    /// Begin transaction.
    /// </summary>
    public static async Task BeginTransactionAsync(this SchemaContext context)
    {
        var dataProvider = context.GetService<IAppDataProvider>() ?? throw new InvalidOperationException(AppErrorCodes.APP_DATA_PROVIDER_NOT_EXIST);
        await dataProvider.BeginTransactionAsync();
        context.SetContextItem(new Dictionary<string, TransactionChangeData>()); // keep track
    }

    /// <summary>
    /// Commit transaction.
    /// </summary>
    public static async Task CommitTransactionAsync(this SchemaContext context, bool noEvent = false)
    {
        var dataProvider = context.GetService<IAppDataProvider>() ?? throw new InvalidOperationException(AppErrorCodes.APP_DATA_PROVIDER_NOT_EXIST);
        var transChangedData = context.GetOrAddContextItem<Dictionary<string, TransactionChangeData>>();

        // Process data field push
        foreach (string target in transChangedData.Keys.ToArray())
            await ProcessDataPush(context, target, transChangedData[target]);

        // Commit
        await dataProvider.CommitTransactionAsync();

        // No data event
        if (noEvent) return;

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
                                DataNode? newValue = change.Value;
                                if (newValue is ArrayNode arr && field.ValueType is ArrayType { Primary.Count: > 0 })
                                {
                                    foreach (DataNode item in arr)
                                        context.RaiseEvent(new AppFieldDataCreateEvent(field.App, field.Name, target), new AppFieldPayload
                                        {
                                            App = field.App,
                                            Field = field.Name,
                                            Target = field.Application.ScopeType != AppScopeType.SystemLevel ? target : null,
                                            Data = item
                                        });
                                }
                                else if (newValue != null)
                                {
                                    context.RaiseEvent(new AppFieldDataCreateEvent(field.App, field.Name, target), new AppFieldPayload
                                    {
                                        App = field.App,
                                        Field = field.Name,
                                        Target = field.Application.ScopeType != AppScopeType.SystemLevel ? target : null,
                                        Data = newValue
                                    });
                                }

                                break;
                            }
                        case TransactionChangeOperation.Modify:
                            {
                                DataNode? changeValues = change.Value;
                                DataNode? originValues = change.Origin;
                                if (changeValues is ArrayNode arr && field.ValueType is ArrayType { Primary.Count: > 0 } type)
                                {
                                    Dictionary<string, DataNode> originMap = [];
                                    if (originValues is ArrayNode oldArr)
                                    {
                                        foreach (DataNode node in oldArr)
                                        {
                                            if (node is not StructNode structNode) continue;
                                            string? key = type.GetPrimaryKey(structNode);
                                            if (string.IsNullOrEmpty(key)) continue;
                                            originMap[key] = structNode;
                                        }
                                    }

                                    // Raise update event or create event
                                    foreach (DataNode node in arr)
                                    {
                                        if (node is StructNode structNode)
                                        {
                                            string? key = type.GetPrimaryKey(structNode);
                                            if (string.IsNullOrEmpty(key)) continue;

                                            if (originMap.Remove(key, out DataNode? o))
                                            {
                                                context.RaiseEvent(new AppFieldDataUpdateEvent(field.App, field.Name, target), new AppFieldUpdatePayload
                                                {
                                                    App = field.App,
                                                    Field = field.Name,
                                                    Target = field.Application.ScopeType != AppScopeType.SystemLevel ? target : null,
                                                    Data = structNode,
                                                    Origin = o
                                                });
                                            }
                                            else
                                            {
                                                context.RaiseEvent(new AppFieldDataCreateEvent(field.App, field.Name, target), new AppFieldPayload
                                                {
                                                    App = field.App,
                                                    Field = field.Name,
                                                    Target = field.Application.ScopeType != AppScopeType.SystemLevel ? target : null,
                                                    Data = structNode,
                                                });
                                            }
                                        }
                                    }

                                    // Raise delete event for remaining origin
                                    foreach (DataNode node in originMap.Values)
                                    {
                                        context.RaiseEvent(new AppFieldDataDeleteEvent(field.App, field.Name, target), new AppFieldPayload
                                        {
                                            App = field.App,
                                            Field = field.Name,
                                            Target = field.Application.ScopeType != AppScopeType.SystemLevel ? target : null,
                                            Data = node
                                        });
                                    }
                                }
                                else if (changeValues != null)
                                {
                                    if (originValues == null)
                                        context.RaiseEvent(new AppFieldDataCreateEvent(field.App, field.Name, target), new AppFieldPayload
                                        {
                                            App = field.App,
                                            Field = field.Name,
                                            Target = field.Application.ScopeType != AppScopeType.SystemLevel ? target : null,
                                            Data = changeValues,
                                        });
                                    else
                                        context.RaiseEvent(new AppFieldDataUpdateEvent(field.App, field.Name, target), new AppFieldUpdatePayload
                                        {
                                            App = field.App,
                                            Field = field.Name,
                                            Target = field.Application.ScopeType != AppScopeType.SystemLevel ? target : null,
                                            Data = changeValues,
                                            Origin = originValues
                                        });
                                }
                                break;
                            }
                        case TransactionChangeOperation.Delete:
                        case TransactionChangeOperation.DropAll:
                            {
                                DataNode? origin = change.Origin;
                                if (origin is ArrayNode arr && field.ValueType is ArrayType { Primary.Count: > 0 })
                                {
                                    foreach (DataNode item in arr)
                                        context.RaiseEvent(new AppFieldDataDeleteEvent(field.App, field.Name, target), new AppFieldPayload
                                        {
                                            App = field.App,
                                            Field = field.Name,
                                            Target = field.Application.ScopeType != AppScopeType.SystemLevel ? target : null,
                                            Data = item
                                        });
                                }
                                else if (origin != null)
                                {
                                    context.RaiseEvent(new AppFieldDataDeleteEvent(field.App, field.Name, target), new AppFieldPayload
                                    {
                                        App = field.App,
                                        Field = field.Name,
                                        Target = field.Application.ScopeType != AppScopeType.SystemLevel ? target : null,
                                        Data = origin
                                    });
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
        var dataProvider = context.GetService<IAppDataProvider>() ?? throw new InvalidOperationException(AppErrorCodes.APP_DATA_PROVIDER_NOT_EXIST);
        await dataProvider.RollbackTransactionAsync();
        context.SetContextItem<Dictionary<string, TransactionChangeData>>(null);
    }

    // Process the data push
    static async Task ProcessDataPush(SchemaContext context, string target, TransactionChangeData changeData, bool pushAllFields = false, AppFieldType? pushNode = null)
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
                if (appNode != null && appNode.GetFields().Any())
                {
                    // Add all the base fields with observers and no function
                    baseFields.AddRange(appNode.GetFields().Where(f => f.PushSource == null && f.HasObserver));
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
            root = new FieldDataPushLevel { Fields = { pushNode } };
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
                         .Where(n => n is { EnableDynamicTable: true, IsForeignView: false }))
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

            // no next level
            if (next.Fields.Count <= 0) break;
            
            // Link the levels
            if (curr != null)
                curr.Next = next;
            else
                root = next;
            curr = next;

            baseFields = next.Fields.Where(p => p.HasObserver).ToList();
        }

        #endregion

        // Process data push
        while (root?.Fields.Count is > 0)
        {
            foreach (AppFieldType field in root.Fields)
            {
                if (field.PushSource == null || field.PushFuncSchema == null) continue;

                using var stack = context.StackAccess(field.App, target);
                
                // Gather the field change infos
                Dictionary<AppFieldType, (DataPushThirdFieldInfo? ThirdInfo, 
                    Dictionary<string, StructNode> Origins, 
                    Dictionary<string, StructNode> Updates, 
                    Dictionary<string, StructNode> UnChanged)> fieldChangeInfos = [];

                if (field.ThirdPushFields is { Length: > 0 })
                {
                    foreach (DataPushThirdFieldInfo thirdInfo in field.ThirdPushFields)
                    {
                        // No push key, skip for now, @TODO: need full push if meet this case
                        if (thirdInfo.PushKeys.Count == 0)
                        {
                            context.LogWarning($"The third push field {thirdInfo.Field} of field {field.App}.{field.Name} has no push keys, skip the third field change detection.");
                            continue;
                        }
                        
                        // Check third app field changes
                        AppFieldType? thirdAppField = field.Application.GetField(thirdInfo.Field);
                        if (thirdAppField == null) continue;
                        GatherFieldChangeInfos(thirdAppField, thirdInfo);
                    }
                }
                
                // Fetch effect push source data
                var pushSourceChangeInfo = GatherFieldChangeInfos(field.PushSource);

                // Fetch effect data from third app fields
                for (int i = (field.ThirdPushFields?.Length ?? 0) - 1; i >= 0; i--)
                {
                    DataPushThirdFieldInfo thirdInfo = field.ThirdPushFields![i];
                    AppFieldType? thirdAppField = field.Application.GetField(thirdInfo.Field);
                    if (thirdAppField == null || !fieldChangeInfos.TryGetValue(thirdAppField, out var changeInfos) || 
                        changeInfos.Origins.Count == 0 && changeInfos.Updates.Count == 0 && changeInfos.UnChanged.Count == 0) continue;
                    
                    foreach (IGrouping<string, DataPushPrimaryFieldAccess> item in thirdInfo.PrimaryMap.OfType<DataPushPrimaryFieldAccess>().GroupBy(p => p.AppField ?? field.PushSource.Name))
                    {
                        AppFieldType fromField = field.Application.GetField(item.Key)!;
                        AppSchemaDataFilter? filter = null;
                        int keyCount = item.Count();
                        switch (keyCount)
                        {
                            // Not possible
                            case 0:
                                throw new InvalidOperationException($"The primary map of third push field {thirdInfo.Field} in field {field.App}.{field.Name} has no valid primary field.");
                            
                            // Use contains, normal case
                            case 1:
                            {
                                ArrayNode? keysNode = CombineField(changeInfos, item.First().Key);
                                if (keysNode == null || keysNode.Count == 0) continue;

                                filter = new AppSchemaDataFilterBinary(LogicType.Contains,
                                    new AppSchemaDataFilterValue(keysNode),
                                    new AppSchemaDataFilterField(item.First().DataField));
                                break;
                            }
                            
                            // Use key contains, complex case
                            default:
                            {
                                // Try Combine keys first
                                HashSet<string> existedKeys = [];
                                AppSchemaDataFilter? buildMapCase(IEnumerable<StructNode> nodes)
                                {
                                    if (existedKeys.Count > MAX_COMBINE_CASE_COUNT) return null;

                                    AppSchemaDataFilter? combineCase = null;
                                    foreach (StructNode node in nodes)
                                    {
                                        AppSchemaDataFilter? caseFilter = null;
                                        string[] keys = new string[keyCount];
                                        int keyIdx = 0;
                                        foreach (DataPushPrimaryFieldAccess map in item)
                                        {
                                            DataNode? keyNode = node.GetAccessValue(map.Key);
                                            if (keyNode == null || keyNode.IsEmpty) // cover case, impossible
                                            {
                                                caseFilter = null;
                                                break;
                                            }
                                            var kFilter = new AppSchemaDataFilterBinary(LogicType.Equal,
                                                new AppSchemaDataFilterField(map.DataField),
                                                new AppSchemaDataFilterValue(keyNode)
                                            );
                                            caseFilter = caseFilter != null
                                                ? new AppSchemaDataFilterBinary(LogicType.AndAlso, caseFilter, kFilter)
                                                : kFilter;
                                            keys[keyIdx++] = keyNode.GetValue<string>()!;
                                        }

                                        if (caseFilter == null || !existedKeys.Add(string.Join(':', keys))) continue;

                                        combineCase = combineCase != null
                                            ? new AppSchemaDataFilterBinary(LogicType.OrElse, combineCase, caseFilter)
                                            : caseFilter;
                                    }

                                    return combineCase;
                                }

                                AppSchemaDataFilter? caseFilters = buildMapCase(changeInfos.Origins.Values);
                                if (caseFilters != null)
                                    filter = filter != null ? new AppSchemaDataFilterBinary(LogicType.OrElse, filter, caseFilters) : caseFilters;

                                caseFilters = buildMapCase(changeInfos.Updates.Values);
                                if (caseFilters != null)
                                    filter = filter != null ? new AppSchemaDataFilterBinary(LogicType.OrElse, filter, caseFilters) : caseFilters;

                                caseFilters = buildMapCase(changeInfos.UnChanged.Values);
                                if (caseFilters != null)
                                    filter = filter != null ? new AppSchemaDataFilterBinary(LogicType.OrElse, filter, caseFilters) : caseFilters;
                                                                
                                // Use contains instead of combine key cases
                                if (filter == null || existedKeys.Count > MAX_COMBINE_CASE_COUNT)
                                {
                                    filter = null;
                                    foreach (DataPushPrimaryFieldAccess map in item)
                                    {
                                        ArrayNode? keysNode = CombineField(changeInfos, map.Key);
                                        if (keysNode == null || keysNode.Count == 0) continue;

                                        AppSchemaDataFilterBinary kFilter = new AppSchemaDataFilterBinary(LogicType.Contains,
                                            new AppSchemaDataFilterValue(keysNode),
                                            new AppSchemaDataFilterField(map.DataField));
                                        filter = filter != null ? new AppSchemaDataFilterBinary(LogicType.AndAlso, filter, kFilter) : kFilter;
                                    }
                                }
                                break;
                            }
                        }
                        
                        // Fetch data
                        if (filter == null || !fieldChangeInfos.TryGetValue(fromField, out var tarChangeInfo)) continue;
                        
                        // Fill unchange data
                        ArrayNode? fetchData = await context.GetSchemaDataAsync(thirdAppField.App, fromField.Name,
                            target, AppSchemaDataResult.List, filter) as ArrayNode;
                        if (fetchData == null) continue;
                        
                        // Fill unchange data
                        DynamicTableSchema schema = fromField.GetDynamicTableSchema(context);
                        foreach (DataNode node in fetchData)
                        {
                            if (node is not StructNode structNode) continue;
                            string? key = schema.GetPrimaryKey(structNode);
                            if (string.IsNullOrEmpty(key)) continue;
                            if (tarChangeInfo.Origins.ContainsKey(key) || tarChangeInfo.Updates.ContainsKey(key)) continue;
                            tarChangeInfo.UnChanged[key] = structNode;
                        }
                    }
                }

                // Init push data
                List<DataNode?[]> originsPush = InitPushData(pushSourceChangeInfo.Origins.Values, pushSourceChangeInfo.UnChanged.Values);
                List<DataNode?[]> updatesPush = InitPushData(pushSourceChangeInfo.Updates.Values, pushSourceChangeInfo.UnChanged.Values);

                // Fill push data
                await FillPushData(originsPush, true);
                await FillPushData(updatesPush);

                // Calc the origin and new push data
                ArrayNode oldResult = await CalcPushData(originsPush);
                ArrayNode newResult = await CalcPushData(updatesPush);
                if (oldResult.IsEmpty && newResult.IsEmpty) continue;
                
                // Save the incremental data
                await context.SaveIncrementalData(field, newResult, oldResult);

                continue;

                // Calc data push
                async Task<ArrayNode> CalcPushData(List<DataNode?[]> pushData)
                {
                    ArrayNode result = new ArrayNode(field.ValueType!);
                    foreach (object?[] args in pushData)
                    {
                        DataNode? ret = await field.PushFunc!.CallAsync<DataNode, DataPushCompileContext>(context, args);
                        if (ret is ArrayNode arr)
                            result.AddRange(arr);
                        else if(ret is not null)
                            result.Add(ret);
                    }
                    return result;
                }

                // Fill the remain push data from third app fields
                async Task FillPushData(List<DataNode?[]> pushData, bool isOrigin = false)
                {
                    for (int i = 0; i < (field.ThirdPushFields?.Length ?? 0); i++)
                    {
                        DataPushThirdFieldInfo thirdInfo = field.ThirdPushFields![i];
                        AppFieldType? thirdAppField = field.Application.GetField(thirdInfo.Field);
                        if (thirdAppField == null || !fieldChangeInfos.TryGetValue(thirdAppField, out var thirdChangeInfos)) continue;
                        DynamicTableSchema schema = thirdAppField.GetDynamicTableSchema(context);
                        
                        // Fill data
                        Dictionary<string, (DataNode[], List<DataNode?[]>)> loadingMap = [];
                        foreach (var pushItem in pushData)
                        {
                            DataNode?[] keysNodes = new DataNode?[thirdInfo.PrimaryMap.Length];
                            for (int k = 0; k < thirdInfo.PrimaryMap.Length; k++)
                            {
                                switch (thirdInfo.PrimaryMap[k])
                                {
                                    case DataPushPrimaryFieldAccess access:
                                        StructNode? owner = pushItem.ElementAtOrDefault(access.ArgIndex) as StructNode;
                                        keysNodes[k] = owner?.GetAccessValue(access.DataField);
                                        break;
                                    
                                    case DataPushPrimaryConstant constant:
                                        keysNodes[k] = constant.Value;
                                        break;
                                }
                            }
                            if (keysNodes.Any(p => p is null || p.IsEmpty)) continue; // pass if any key is null
                            string? key = schema.GetPrimaryKey(keysNodes);
                            if (string.IsNullOrEmpty(key)) continue;
                            
                            // Try get exist data
                            StructNode? thirdData = (isOrigin ? thirdChangeInfos.Origins : thirdChangeInfos.Updates).GetValueOrDefault(key);
                            thirdData ??= thirdChangeInfos.UnChanged.GetValueOrDefault(key);

                            if (thirdData == null)
                            {
                                // require load from data source
                                if (!loadingMap.TryGetValue(key, out var list))
                                {
                                    list = (keysNodes.Select(p => p!).ToArray(), new List<DataNode?[]>());
                                    loadingMap[key] = list;
                                }
                                list.Item2.Add(pushItem);
                            }
                            else
                            {
                                pushItem[i + 1] = thirdData;
                            }
                        }
                        
                        // Load missing data
                        if (loadingMap.Count != 0)
                        {
                            AppSchemaDataFilter? filter = null;
                            var primaries = (thirdAppField.ValueType as ArrayType)!.Primary!;
                            
                            // Use contains
                            if (loadingMap.Count > MAX_COMBINE_CASE_COUNT)
                            {
                                for (int k = 0; k < primaries.Count; k++)
                                {
                                    ArrayNode? keysNode = null;
                                    foreach (var item in loadingMap.Values)
                                    {
                                        keysNode ??= new ArrayNode(item.Item1[k].Type);
                                        keysNode.Add(item.Item1[k]);
                                    }
                                    if (keysNode == null) continue;

                                    AppSchemaDataFilterBinary kFilter = new AppSchemaDataFilterBinary(LogicType.Contains,
                                        new AppSchemaDataFilterValue(keysNode),
                                        new AppSchemaDataFilterField(primaries[k]));
                                    filter = filter != null ? new AppSchemaDataFilterBinary(LogicType.AndAlso, filter, kFilter) : kFilter;
                                }
                            }
                            // Use key contains
                            else
                            {
                                foreach (var item in loadingMap.Values)
                                {
                                    AppSchemaDataFilter? caseFilter = null;
                                    
                                    for (int k = 0; k < primaries.Count; k++)
                                    {
                                        AppSchemaDataFilter kMatch = new AppSchemaDataFilterBinary(LogicType.Equal,
                                            new AppSchemaDataFilterField(primaries[k]),
                                            new AppSchemaDataFilterValue(item.Item1[k]));

                                        caseFilter = caseFilter != null
                                            ? new AppSchemaDataFilterBinary(LogicType.AndAlso, caseFilter, kMatch)
                                            : kMatch;
                                    }
                                    
                                    if (caseFilter == null) continue;
                                    filter = filter != null
                                        ? new AppSchemaDataFilterBinary(LogicType.OrElse, filter, caseFilter)
                                        : caseFilter;
                                }
                            }

                            // Fetch and fill unchange data
                            if (filter != null && await context.GetSchemaDataAsync(thirdAppField.App, thirdAppField.Name,
                                    target, AppSchemaDataResult.List, filter) is ArrayNode fetchData)
                            {
                                foreach (DataNode node in fetchData)
                                {
                                    if (node is not StructNode structNode) continue;
                                    string? key = schema.GetPrimaryKey(structNode);
                                    if (string.IsNullOrEmpty(key)) continue;
                                    
                                    // Fill to push data
                                    if (loadingMap.TryGetValue(key, out var list))
                                    {
                                        foreach (DataNode?[] pushItem in list.Item2)
                                            pushItem[i + 1] = structNode;
                                    }
                                }
                            }
                        }
                    }
                }

                // Build push data
                List<DataNode?[]> InitPushData(IEnumerable<StructNode> nodes, IEnumerable<StructNode> unchange)
                {
                    List<DataNode?[]> pushData = [];
                    foreach (StructNode node in nodes)
                    {
                        DataNode?[] pushItems = new DataNode?[field.PushFuncSchema!.Args.Length];
                        pushItems[0] = node;
                        pushData.Add(pushItems);
                    }
                    foreach (StructNode node in unchange)
                    {
                        DataNode?[] pushItems = new DataNode?[field.PushFuncSchema!.Args.Length];
                        pushItems[0] = node;
                        pushData.Add(pushItems);
                    }
                    return pushData;
                }

                ArrayNode? CombineField((DataPushThirdFieldInfo? ThirdInfo,
                    Dictionary<string, StructNode> Origins,
                    Dictionary<string, StructNode> Updates,
                    Dictionary<string, StructNode> UnChanged) changeInfo, string keyField)
                {
                    HashSet<string> keys = [];
                    ArrayNode? keysNode = null;
                    void AddKeys(IEnumerable<StructNode> nodes)
                    {
                        foreach (DataNode? key in nodes.Select(node => node.GetAccessValue(keyField)))
                        {
                            if (key is not { IsEmpty: false }) continue;
                            var keyStr = key.GetValue<string>() ?? string.Empty;
                            if (!string.IsNullOrEmpty(keyStr) && keys.Add(keyStr))
                            {
                                keysNode ??= new ArrayNode(key.Type);
                                keysNode.Add(key);
                            }
                        }
                    }
                    AddKeys(changeInfo.Origins.Values);
                    AddKeys(changeInfo.Updates.Values);
                    AddKeys(changeInfo.UnChanged.Values);
                    return keysNode;
                }

                (DataPushThirdFieldInfo? ThirdInfo, 
                    Dictionary<string, StructNode> Origins, 
                    Dictionary<string, StructNode> Updates, 
                    Dictionary<string, StructNode> UnChanged) GatherFieldChangeInfos(AppFieldType appField, DataPushThirdFieldInfo? thirdInfo = null)
                {
                    List<FieldDataChangeData>? changes = changeData.Changes.GetValueOrDefault(appField);
                    DynamicTableSchema schema = appField.GetDynamicTableSchema(context);

                    Dictionary<string, StructNode> origins = [];
                    Dictionary<string, StructNode> updates = [];

                    if (changes != null)
                    {
                        foreach (FieldDataChangeData change in changes)
                        {
                            // for origin
                            switch (change.Origin)
                            {
                                case ArrayNode ArrayNode:
                                {
                                    foreach (DataNode node in ArrayNode)
                                    {
                                        if (node is not StructNode StructNode) continue;
                                        string? key = schema.GetPrimaryKey(StructNode);
                                        if (string.IsNullOrEmpty(key)) continue;
                                        origins[key] = StructNode;
                                    }

                                    break;
                                }
                                case StructNode StructNode:
                                {
                                    string? key = schema.GetPrimaryKey(StructNode);
                                    if (string.IsNullOrEmpty(key)) continue;
                                    origins[key] = StructNode;
                                    break;
                                }
                            }

                            // For new
                            switch (change.Value)
                            {
                                case ArrayNode ArrayNode:
                                {
                                    foreach (DataNode node in ArrayNode)
                                    {
                                        if (node is not StructNode StructNode) continue;
                                        string? key = schema.GetPrimaryKey(StructNode);
                                        if (string.IsNullOrEmpty(key)) continue;
                                        StructNode? origin = origins.GetValueOrDefault(key);

                                        // check push key changes
                                        if (origin == null || thirdInfo == null)
                                            updates[key] = StructNode;
                                        else
                                        {
                                            bool isEqual = true;
                                            foreach (string pushKey in thirdInfo.PushKeys)
                                            {
                                                DataNode? oVal = origin.GetAccessValue(pushKey);
                                                DataNode? nVal = StructNode.GetAccessValue(pushKey);
                                                if (oVal is { IsEmpty: false } || nVal is { IsEmpty: false })
                                                {
                                                    if (oVal == null || oVal.IsEmpty || nVal == null || nVal.IsEmpty)
                                                    {
                                                        isEqual = false;
                                                        break;
                                                    }
                                                    else if (!oVal.Equals(nVal))
                                                    {
                                                        isEqual = false;
                                                        break;
                                                    }
                                                }
                                            }

                                            if (!isEqual)
                                            {
                                                updates[key] = StructNode;
                                            }
                                            else
                                                origins.Remove(key); // No change
                                        }
                                    }

                                    break;
                                }
                                case StructNode StructNode:
                                {
                                    string? key = schema.GetPrimaryKey(StructNode);
                                    if (string.IsNullOrEmpty(key)) continue;
                                    StructNode? origin = origins.GetValueOrDefault(key);

                                    // check push key changes
                                    if (origin == null || thirdInfo == null || (from pushKey in thirdInfo.PushKeys
                                            let oVal = origin.GetAccessValue(pushKey)
                                            let nVal = StructNode.GetAccessValue(pushKey)
                                            where oVal is { IsEmpty: false } || nVal is { IsEmpty: false }
                                            where oVal == null || nVal == null || !oVal.Equals(nVal)
                                            select oVal).Any())
                                        updates[key] = StructNode;
                                    else
                                        origins.Remove(key); // No change
                                    break;
                                }
                            }
                        }
                    }

                    var result = (thirdInfo, origins, updates, new Dictionary<string, StructNode>());
                    fieldChangeInfos.Add(appField, result);
                    return result;
                }
            }

            // Process next level
            root = root.Next;
        }
    }

    /// <summary>
    /// Save the incremental data
    /// </summary>
    static async Task SaveIncrementalData(this SchemaContext context, AppFieldType field, DataNode? newResult, DataNode? oldResult)
    {
        // Join the result
        DataNode? result = null;
        switch (field.ValueType)
        {
            case EnumType:
                {
                    DataCombineType method = field.Combine ?? DataCombineType.Newest;
                    (DataNode? origin, _) = await context.GetAppFieldDataAsync(field, AppSchemaDataResult.List);
                    DataNode? now = DataCombineTypeExtensions.GroupJoin(newResult, method);

                    // Update with join method
                    switch (method)
                    {
                        case DataCombineType.Newest:
                            {
                                result = now is { IsEmpty: false } ? now : origin;
                                break;
                            }
                        case DataCombineType.Oldest:
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
                    DataCombineType method = field.Combine ?? (scalar is IntType or DecimalType ? DataCombineType.Sum : DataCombineType.Newest);

                    // Part
                    (DataNode? origin, _) = await context.GetAppFieldDataAsync(field, AppSchemaDataResult.List);
                    DataNode? old = scalar.GroupJoin(oldResult, method);
                    DataNode? now = scalar.GroupJoin(newResult, method);

                    // Update with join method
                    switch (method)
                    {
                        case DataCombineType.Newest:
                            {
                                result = now;
                                break;
                            }
                        case DataCombineType.Oldest:
                            {
                                result = origin is { IsEmpty: false } ? origin : now;
                                break;
                            }
                        case DataCombineType.Sum:
                        case DataCombineType.Count:
                            {
                                result = field.ValueType.From(
                                    (origin is { IsEmpty: false } ? origin.GetValue<decimal>() : 0m) +
                                    (now is { IsEmpty: false } ? now.GetValue<decimal>() : 0m) -
                                    (old is { IsEmpty: false } ? old.GetValue<decimal>() : 0m)
                                );
                            }
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                    break;
                }
            case StructType @struct:
                {
                    // Gets the join method map
                    Dictionary<string, DataCombineType> joinMethodMap = new();

                    // Default join
                    foreach (var f in @struct.GetFields())
                    {
                        if (f.Type is ScalarType s)
                            joinMethodMap[f.Name] = field.Combines?.FirstOrDefault(o => o.Field.Equals(f.Name, StringComparison.OrdinalIgnoreCase))?.Type
                                ?? (s is IntType or DecimalType ? DataCombineType.Sum : DataCombineType.Newest);
                    }

                    // Gets the result
                    (DataNode? origin, _) = await context.GetAppFieldDataAsync(field, AppSchemaDataResult.List);
                    DataNode? old = @struct.GroupJoin(oldResult, joinMethodMap);
                    DataNode? now = @struct.GroupJoin(newResult, joinMethodMap);

                    // Update with join method
                    if ((origin == null || origin.IsEmpty) && (old == null || old.IsEmpty))
                    {
                        result = now;
                    }
                    else
                    {
                        StructNode final = new StructNode(@struct);
                        foreach (var nodeField in @struct.GetFields())
                        {
                            DataNode? originFld = origin is StructNode os ? os.GetAccessValue(nodeField.Name) : null;
                            DataNode? oldFld = old is StructNode ols ? ols.GetAccessValue(nodeField.Name) : null;
                            DataNode? nowFld = now is StructNode ns ? ns.GetAccessValue(nodeField.Name) : null;

                            switch (joinMethodMap.GetValueOrDefault(nodeField.Name, DataCombineType.Newest))
                            {
                                case DataCombineType.Newest:
                                    {
                                        final[nodeField.Name] = nowFld is { IsEmpty: false } ? nowFld : originFld;
                                        break;
                                    }
                                case DataCombineType.Oldest:
                                    {
                                        final[nodeField.Name] = originFld is { IsEmpty: false } ? originFld : nowFld;
                                        break;
                                    }
                                case DataCombineType.Sum when nodeField.Type is IntType or  DecimalType:
                                case DataCombineType.Count when nodeField.Type is IntType:
                                    {
                                        final[nodeField.Name] = nodeField.Type.From(
                                            (originFld is { IsEmpty: false } ? originFld.GetValue<decimal>() : 0m) +
                                            (nowFld is { IsEmpty: false } ? nowFld.GetValue<decimal>() : 0m) -
                                            (oldFld is { IsEmpty: false } ? oldFld.GetValue<decimal>() : 0m)
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
            case ArrayType { Element: EnumType or ScalarType }:
                {
                    result = newResult;
                    break;
                }
            case ArrayType { Element: StructType structNode, Primary: { Count: > 0 } } array:
                {
                    // Gets the join method map
                    Dictionary<string, DataCombineType> joinMethodMap = new();

                    // Gets the value fields
                    List<string> valueFields = new();
                    Dictionary<string, Runtime.ValueType> primaryNodes = new();
                    foreach (var fieldType in structNode.GetFields())
                    {
                        if (!array.Primary.Contains(fieldType.Name))
                        {
                            valueFields.Add(fieldType.Name);

                            if (fieldType.Type is ScalarType s)
                            {
                                joinMethodMap[fieldType.Name] = s is IntType or DecimalType ? DataCombineType.Sum : DataCombineType.Newest;
                            }
                        }
                        else
                            primaryNodes.Add(fieldType.Name, fieldType.Type!);
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
                    oldResult = oldResult is ArrayNode oldArr ? oldArr.FilterByPrimaryKeys(array.Primary) : oldResult;
                    newResult = newResult is ArrayNode newArr ? newArr.FilterByPrimaryKeys(array.Primary) : newResult;
                    
                    // Group join the old & now data
                    Dictionary<string, StructNode> oldMap = DataCombineTypeExtensions.GroupJoinObjectMap(array, oldResult, joinMethodMap);
                    Dictionary<string, StructNode> nowMap = DataCombineTypeExtensions.GroupJoinObjectMap(array, newResult, joinMethodMap);

                    // Query the original data
                    var origins = await context.GetAppFieldDataAsync(field,oldMap.Values.Concat(nowMap.Values));
                    
                    // Gets the original data
                    Dictionary<string, StructNode> resultMap = new Dictionary<string, StructNode>();
                    if (origins is ArrayNode arr)
                    {
                        foreach (DataNode token in arr)
                        {
                            if (token is not StructNode obj) continue;
                            string? key = array.GetPrimaryKey(obj);
                            if (string.IsNullOrWhiteSpace(key)) continue;
                            resultMap[key] = obj;
                        }
                    }

                    // Generate the result map
                    var keys = new HashSet<string>(oldMap.Keys.Concat(nowMap.Keys));
                    foreach (string key in keys)
                    {
                        if (resultMap.TryGetValue(key, out var res1))
                        {
                            oldMap.TryGetValue(key, out StructNode? old);
                            nowMap.TryGetValue(key, out StructNode? now);
                            foreach (string s in valueFields)
                            {
                                DataNode? originFld = res1.GetAccessValue(s);
                                DataNode? oldFld = old?.GetAccessValue(s);
                                DataNode? nowFld = now?.GetAccessValue(s);

                                switch (joinMethodMap.GetValueOrDefault(s, DataCombineType.Newest))
                                {
                                    case DataCombineType.Newest:
                                        if (nowFld is { IsEmpty: false })
                                            res1[s] = nowFld;
                                        break;
                                    case DataCombineType.Oldest:
                                        if (originFld == null || originFld.IsEmpty)
                                            res1[s] = nowFld;
                                        break;
                                    case DataCombineType.Sum:
                                    case DataCombineType.Count:
                                        res1.TrySetFieldValue(s, (originFld is { IsEmpty: false } ? originFld.GetValue<decimal>() : 0m) +
                                            (nowFld is { IsEmpty: false } ? nowFld.GetValue<decimal>() : 0m) -
                                            (oldFld is { IsEmpty: false } ? oldFld.GetValue<decimal>() : 0m));
                                        break;
                                    default:
                                        throw new ArgumentOutOfRangeException();
                                }
                            }
                        }
                        else if (nowMap.TryGetValue(key, out StructNode? res))
                        {
                            resultMap.Add(key, res);
                            if (!oldMap.TryGetValue(key, out StructNode? old)) continue;

                            // Shouldn't be but still handle it
                            foreach (string s in valueFields)
                            {
                                DataNode? oldFld = old?.GetAccessValue(s);
                                DataNode? nowFld = res?.GetAccessValue(s);

                                switch (joinMethodMap.GetValueOrDefault(s, DataCombineType.Newest))
                                {
                                    case DataCombineType.Newest:
                                        if (nowFld == null || nowFld.IsEmpty)
                                            res![s] = oldFld;
                                        break;
                                    case DataCombineType.Oldest:
                                        if (oldFld is { IsEmpty: false })
                                            res![s] = oldFld;
                                        break;
                                    case DataCombineType.Sum:
                                    case DataCombineType.Count:
                                        res?.TrySetFieldValue(s, (nowFld is { IsEmpty: false } ? nowFld.GetValue<decimal>() : 0m) -
                                                   (oldFld is { IsEmpty: false } ? oldFld.GetValue<decimal>() : 0m));
                                        break;
                                    default:
                                        throw new ArgumentOutOfRangeException();
                                }
                            }
                        }
                    }

                    // Convert the map to list, sorted by primary keys
                    List<StructNode> joinObjs = resultMap.Values.ToList();
                    joinObjs.Sort((a, b) =>
                    {
                        foreach (string s in array.Primary)
                        {
                            switch (primaryNodes[s])
                            {
                                case DateType:
                                {
                                    DateTime ad = a.GetAccessValue(s)!.GetValue<DateTime>();
                                    DateTime bd = b.GetAccessValue(s)!.GetValue<DateTime>();
                                    if (!bd.Equals(ad))
                                        return ad.CompareTo(bd);
                                    break;
                                }
                                case DecimalType:
                                {
                                    decimal ad = a.GetAccessValue(s)!.GetValue<decimal>();
                                    decimal bd = b.GetAccessValue(s)!.GetValue<decimal>();
                                    if (ad != bd)
                                        return ad < bd ? -1 : 1;
                                    break;
                                }
                                case IntType:
                                {
                                    decimal ad = a.GetAccessValue(s)!.GetValue<long>();
                                    decimal bd = b.GetAccessValue(s)!.GetValue<long>();
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
                    result = field.ValueType.From(joinObjs);
                    break;
                }
        }

        // Save
        await context.SaveFieldDataAsync(field, result, true);
    }

    // Record the changed fields with changed values
    static void OnFieldDataChanged(SchemaContext context,  AppFieldType field, TransactionChangeOperation operation, DataNode? value = null, DataNode? origin = null)
    {
        var transChangedData = context.GetOrAddContextItem<Dictionary<string, TransactionChangeData>>();
        var access = context.GetContextItem<Access>();
        string target = access!.Target ?? Guid.Empty.ToString();
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
