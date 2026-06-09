using Microsoft.Extensions.Logging;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Function;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using System.Text.Json.Nodes;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.Schema;
// ReSharper disable InconsistentNaming

namespace SchemaNode.Components;

public static class AppDataTransactionExtension
{
    #region Save

    /// <summary>
    /// Save field data
    /// </summary>
    public static Task<bool> SaveFieldDataAsync(this SchemaContext context, AppFieldType field, JsonNode? value = null)
        => context.SaveFieldDataAsync(field, field.SchemaType!.CreateNode(value) ?? throw new NotSupportedException());

    /// <summary>
    /// Save the field data by data
    /// </summary>
    public static async Task<bool> SaveFieldDataAsync(this SchemaContext context, AppFieldType field, AnySchemaNode? value = null, bool innerCall = false, bool canAdd = true, bool onlyAdd = false, string[]? overrides = null)
    {
        // no front only & enable & no source ref
        if (!field.EnableDynamicTable || field.IsForeignView) return false;
        if (field.Readonly == true && !innerCall) return false; // readonly can only be set by system

        // Not allow the direct data update
        if (!innerCall && !string.IsNullOrWhiteSpace(field.Func)) return false;
        var dataProvider = context.GetService<IAppDataProvider>() ?? throw new InvalidOperationException(AppErrorCodes.APP_DATA_PROVIDER_NOT_EXIST);

        // Prepare
        DynamicTableSchema schema = await context.PrepareFieldDataAsync(field);

        try
        {
            (bool result, AnySchemaNode? update, AnySchemaNode? origin) = await dataProvider.SaveDynamicTableDataAsync(schema, value, canAdd, onlyAdd, overrides);
            if (result) OnFieldDataChanged(context, field, TransactionChangeOperation.Modify, update, origin);
            return result;
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex.Message);
            throw;
        }
    }

    public static async Task<bool> ClearFieldDataAsync(this SchemaContext context, AppFieldType field, bool innerCall = false)
    {
        // no front only & enable & no source ref
        if (!field.EnableDynamicTable || field.IsForeignView) return false;
        if (field.Readonly == true && !innerCall) return false; // readonly can only be set by system

        var dataProvider = context.GetService<IAppDataProvider>() ?? throw new InvalidOperationException(AppErrorCodes.APP_DATA_PROVIDER_NOT_EXIST);

        // Prepare
        DynamicTableSchema schema = await context.PrepareFieldDataAsync(field);

        try
        {
            (bool result, AnySchemaNode? origin) = await dataProvider.ClearDynamicTableDataAsync(schema);
            if (result) OnFieldDataChanged(context, field, TransactionChangeOperation.DropAll, null, origin);
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
    /// Delete the list from a list-struct type field data
    /// </summary>
    public static async Task<bool> DeleteFieldListDataAsync(this SchemaContext context, AppFieldType field, AnySchemaNode nodes)
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
            if (nodes is ArrayTypeNode { Count: 0 }) return false; // pass if no node to delete
            (bool result, AnySchemaNode? origin) = await dataProvider.DeleteSchemaNodeAsync(schema, nodes);
            if (result) OnFieldDataChanged(context, field, TransactionChangeOperation.Delete, null, origin);
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex.Message);
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
            (bool result, AnySchemaNode? origin) = await dataProvider.DeleteDynamicTableDataAsync(schema, filter);
            if (result) OnFieldDataChanged(context, field, TransactionChangeOperation.Delete, null, origin);
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex.Message);
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
        var transChangedData = context.GetOrCreateContextItem<Dictionary<string, TransactionChangeData>>();

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
                    Dictionary<string, StructTypeNode> Origins, 
                    Dictionary<string, StructTypeNode> Updates, 
                    Dictionary<string, StructTypeNode> UnChanged)> fieldChangeInfos = [];

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
                                ArrayTypeNode? keysNode = CombineField(changeInfos, item.First().Key);
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
                                AppSchemaDataFilter? buildMapCase(IEnumerable<StructTypeNode> nodes)
                                {
                                    if (existedKeys.Count > MAX_COMBINE_CASE_COUNT) return null;

                                    AppSchemaDataFilter? combineCase = null;
                                    foreach (StructTypeNode node in nodes)
                                    {
                                        AppSchemaDataFilter? caseFilter = null;
                                        string[] keys = new string[keyCount];
                                        int keyIdx = 0;
                                        foreach (DataPushPrimaryFieldAccess map in item)
                                        {
                                            AnySchemaNode? keyNode = node.GetField(map.Key);
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
                                            keys[keyIdx++] = keyNode.ToString();
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
                                        ArrayTypeNode? keysNode = CombineField(changeInfos, map.Key);
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
                        ArrayTypeNode? fetchData = await context.GetSchemaDataAsync(thirdAppField.App, fromField.Name,
                            target, AppSchemaDataResult.List, filter) as ArrayTypeNode;
                        if (fetchData == null) continue;
                        
                        // Fill unchange data
                        DynamicTableSchema schema = fromField.Schema ?? fromField.GenDynamicTableSchema();
                        foreach (AnySchemaNode node in fetchData)
                        {
                            if (node is not StructTypeNode structNode) continue;
                            string? key = schema.GetPrimaryKey(structNode);
                            if (string.IsNullOrEmpty(key)) continue;
                            if (tarChangeInfo.Origins.ContainsKey(key) || tarChangeInfo.Updates.ContainsKey(key)) continue;
                            tarChangeInfo.UnChanged[key] = structNode;
                        }
                    }
                }

                // Init push data
                List<AnySchemaNode?[]> originsPush = InitPushData(pushSourceChangeInfo.Origins.Values, pushSourceChangeInfo.UnChanged.Values);
                List<AnySchemaNode?[]> updatesPush = InitPushData(pushSourceChangeInfo.Updates.Values, pushSourceChangeInfo.UnChanged.Values);

                // Fill push data
                await FillPushData(originsPush, true);
                await FillPushData(updatesPush);

                // Calc the origin and new push data
                ArrayTypeNode oldResult = await CalcPushData(originsPush);
                ArrayTypeNode newResult = await CalcPushData(updatesPush);
                if (oldResult.IsEmpty && newResult.IsEmpty) continue;
                
                // Save the incremental data
                await context.SaveIncrementalData(field, newResult, oldResult);

                continue;

                // Calc data push
                async Task<ArrayTypeNode> CalcPushData(List<AnySchemaNode?[]> pushData)
                {
                    ArrayTypeNode result = new ArrayTypeNode(field.SchemaType!);
                    foreach (object?[] args in pushData)
                    {
                        AnySchemaNode? ret = await field.FuncNode!.CallAsync<AnySchemaNode, DataPushCompileContext>(context, args);
                        if (ret is ArrayTypeNode arr)
                            result.AddRange(arr);
                        else if(ret is not null)
                            result.Add(ret);
                    }
                    return result;
                }

                // Fill the remain push data from third app fields
                async Task FillPushData(List<AnySchemaNode?[]> pushData, bool isOrigin = false)
                {
                    for (int i = 0; i < (field.ThirdPushFields?.Length ?? 0); i++)
                    {
                        DataPushThirdFieldInfo thirdInfo = field.ThirdPushFields![i];
                        AppFieldType? thirdAppField = field.Application.GetField(thirdInfo.Field);
                        if (thirdAppField == null || !fieldChangeInfos.TryGetValue(thirdAppField, out var thirdChangeInfos)) continue;
                        DynamicTableSchema schema = thirdAppField.Schema ?? thirdAppField.GenDynamicTableSchema();
                        
                        // Fill data
                        Dictionary<string, (AnySchemaNode[], List<AnySchemaNode?[]>)> loadingMap = [];
                        foreach (var pushItem in pushData)
                        {
                            AnySchemaNode?[] keysNodes = new AnySchemaNode?[thirdInfo.PrimaryMap.Length];
                            for (int k = 0; k < thirdInfo.PrimaryMap.Length; k++)
                            {
                                switch (thirdInfo.PrimaryMap[k])
                                {
                                    case DataPushPrimaryFieldAccess access:
                                        StructTypeNode? owner = pushItem.ElementAtOrDefault(access.ArgIndex) as StructTypeNode;
                                        keysNodes[k] = owner?.GetField(access.DataField);
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
                            StructTypeNode? thirdData = (isOrigin ? thirdChangeInfos.Origins : thirdChangeInfos.Updates).GetValueOrDefault(key);
                            thirdData ??= thirdChangeInfos.UnChanged.GetValueOrDefault(key);

                            if (thirdData == null)
                            {
                                // require load from data source
                                if (!loadingMap.TryGetValue(key, out var list))
                                {
                                    list = (keysNodes.Select(p => p!).ToArray(), new List<AnySchemaNode?[]>());
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
                            string[] primaries = (thirdAppField.SchemaType as ArrayType)!.Primary!;
                            
                            // Use contains
                            if (loadingMap.Count > MAX_COMBINE_CASE_COUNT)
                            {
                                for (int k = 0; k < primaries.Length; k++)
                                {
                                    ArrayTypeNode? keysNode = null;
                                    foreach (var item in loadingMap.Values)
                                    {
                                        keysNode ??= new ArrayTypeNode(item.Item1[k].SchemaType);
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
                                    
                                    for (int k = 0; k < primaries.Length; k++)
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
                                    target, AppSchemaDataResult.List, filter) is ArrayTypeNode fetchData)
                            {
                                foreach (AnySchemaNode node in fetchData)
                                {
                                    if (node is not StructTypeNode structNode) continue;
                                    string? key = schema.GetPrimaryKey(structNode);
                                    if (string.IsNullOrEmpty(key)) continue;
                                    
                                    // Fill to push data
                                    if (loadingMap.TryGetValue(key, out var list))
                                    {
                                        foreach (AnySchemaNode?[] pushItem in list.Item2)
                                            pushItem[i + 1] = structNode;
                                    }
                                }
                            }
                        }
                    }
                }

                // Build push data
                List<AnySchemaNode?[]> InitPushData(IEnumerable<StructTypeNode> nodes, IEnumerable<StructTypeNode> unchange)
                {
                    List<AnySchemaNode?[]> pushData = [];
                    foreach (StructTypeNode node in nodes)
                    {
                        AnySchemaNode?[] pushItems = new AnySchemaNode?[field.PushFuncSchema!.Args.Length];
                        pushItems[0] = node;
                        pushData.Add(pushItems);
                    }
                    foreach (StructTypeNode node in unchange)
                    {
                        AnySchemaNode?[] pushItems = new AnySchemaNode?[field.PushFuncSchema!.Args.Length];
                        pushItems[0] = node;
                        pushData.Add(pushItems);
                    }
                    return pushData;
                }

                ArrayTypeNode? CombineField((DataPushThirdFieldInfo? ThirdInfo,
                    Dictionary<string, StructTypeNode> Origins,
                    Dictionary<string, StructTypeNode> Updates,
                    Dictionary<string, StructTypeNode> UnChanged) changeInfo, string keyField)
                {
                    HashSet<string> keys = [];
                    ArrayTypeNode? keysNode = null;
                    void AddKeys(IEnumerable<StructTypeNode> nodes)
                    {
                        foreach (AnySchemaNode? key in nodes.Select(node => node.GetField(keyField)))
                        {
                            if (key is not { IsEmpty: false }) continue;
                            var keyStr = key.ToValue<string>() ?? string.Empty;
                            if (!string.IsNullOrEmpty(keyStr) && keys.Add(keyStr))
                            {
                                keysNode ??= new ArrayTypeNode(key.SchemaType);
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
                    Dictionary<string, StructTypeNode> Origins, 
                    Dictionary<string, StructTypeNode> Updates, 
                    Dictionary<string, StructTypeNode> UnChanged) GatherFieldChangeInfos(AppFieldType appField, DataPushThirdFieldInfo? thirdInfo = null)
                {
                    List<FieldDataChangeData>? changes = changeData.Changes.GetValueOrDefault(appField);
                    DynamicTableSchema schema = appField.Schema ?? appField.GenDynamicTableSchema();

                    Dictionary<string, StructTypeNode> origins = [];
                    Dictionary<string, StructTypeNode> updates = [];

                    if (changes != null)
                    {
                        foreach (FieldDataChangeData change in changes)
                        {
                            // for origin
                            switch (change.Origin)
                            {
                                case ArrayTypeNode arrayTypeNode:
                                {
                                    foreach (AnySchemaNode node in arrayTypeNode)
                                    {
                                        if (node is not StructTypeNode structTypeNode) continue;
                                        string? key = schema.GetPrimaryKey(structTypeNode);
                                        if (string.IsNullOrEmpty(key)) continue;
                                        origins[key] = structTypeNode;
                                    }

                                    break;
                                }
                                case StructTypeNode structTypeNode:
                                {
                                    string? key = schema.GetPrimaryKey(structTypeNode);
                                    if (string.IsNullOrEmpty(key)) continue;
                                    origins[key] = structTypeNode;
                                    break;
                                }
                            }

                            // For new
                            switch (change.Value)
                            {
                                case ArrayTypeNode arrayTypeNode:
                                {
                                    foreach (AnySchemaNode node in arrayTypeNode)
                                    {
                                        if (node is not StructTypeNode structTypeNode) continue;
                                        string? key = schema.GetPrimaryKey(structTypeNode);
                                        if (string.IsNullOrEmpty(key)) continue;
                                        StructTypeNode? origin = origins.GetValueOrDefault(key);

                                        // check push key changes
                                        if (origin == null || thirdInfo == null)
                                            updates[key] = structTypeNode;
                                        else
                                        {
                                            bool isEqual = true;
                                            foreach (string pushKey in thirdInfo.PushKeys)
                                            {
                                                AnySchemaNode? oVal = origin.GetField(pushKey);
                                                AnySchemaNode? nVal = structTypeNode.GetField(pushKey);
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
                                                updates[key] = structTypeNode;
                                            }
                                            else
                                                origins.Remove(key); // No change
                                        }
                                    }

                                    break;
                                }
                                case StructTypeNode structTypeNode:
                                {
                                    string? key = schema.GetPrimaryKey(structTypeNode);
                                    if (string.IsNullOrEmpty(key)) continue;
                                    StructTypeNode? origin = origins.GetValueOrDefault(key);

                                    // check push key changes
                                    if (origin == null || thirdInfo == null || (from pushKey in thirdInfo.PushKeys
                                            let oVal = origin.GetField(pushKey)
                                            let nVal = structTypeNode.GetField(pushKey)
                                            where oVal is { IsEmpty: false } || nVal is { IsEmpty: false }
                                            where oVal == null || nVal == null || !oVal.Equals(nVal)
                                            select oVal).Any())
                                        updates[key] = structTypeNode;
                                    else
                                        origins.Remove(key); // No change
                                    break;
                                }
                            }
                        }
                    }

                    var result = (thirdInfo, origins, updates, new Dictionary<string, StructTypeNode>());
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
    static async Task SaveIncrementalData(this SchemaContext context, AppFieldType field, AnySchemaNode? newResult, AnySchemaNode? oldResult)
    {
        // Join the result
        AnySchemaNode? result = null;
        switch (field.SchemaType)
        {
            case EnumType:
                {
                    DataCombineType method = field.Combine ?? DataCombineType.Assign;
                    (AnySchemaNode? origin, _) = await context.GetAppFieldDataAsync(field, AppSchemaDataResult.List);
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
                    (AnySchemaNode? origin, _) = await context.GetAppFieldDataAsync(field, AppSchemaDataResult.List);
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
                    foreach (StructFieldSchema f in @struct.Fields)
                    {
                        if (f.SchemaType is ScalarType s)
                            joinMethodMap[f.Name] = field.Combines?.FirstOrDefault(o => o.Field.Equals(f.Name, StringComparison.OrdinalIgnoreCase))?.Type
                                ?? (s.IsNumber ? DataCombineType.Sum : DataCombineType.Assign);
                    }

                    // Gets the result
                    (AnySchemaNode? origin, _) = await context.GetAppFieldDataAsync(field, AppSchemaDataResult.List);
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
                        foreach (StructFieldSchema nodeField in @struct.Fields)
                        {
                            AnySchemaNode? originFld = origin is StructTypeNode os ? os.GetField(nodeField.Name) : null;
                            AnySchemaNode? oldFld = old is StructTypeNode ols ? ols.GetField(nodeField.Name) : null;
                            AnySchemaNode? nowFld = now is StructTypeNode ns ? ns.GetField(nodeField.Name) : null;

                            switch (joinMethodMap.GetValueOrDefault(nodeField.Name, DataCombineType.Assign))
                            {
                                case DataCombineType.Assign:
                                    {
                                        final[nodeField.Name] = nowFld is { IsEmpty: false } ? nowFld : originFld;
                                        break;
                                    }
                                case DataCombineType.Init:
                                    {
                                        final[nodeField.Name] = originFld is { IsEmpty: false } ? originFld : nowFld;
                                        break;
                                    }
                                case DataCombineType.Sum when nodeField.SchemaType is ScalarType { IsNumber: true }:
                                case DataCombineType.Count when nodeField.SchemaType is ScalarType { IsNumber: true }:
                                    {
                                        final[nodeField.Name] = nodeField.SchemaType.CreateNode(
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
                    Dictionary<string, AnySchemaType> primaryNodes = new();
                    foreach (StructFieldSchema fieldType in structNode.Fields)
                    {
                        if (!array.Primary.Contains(fieldType.Name))
                        {
                            valueFields.Add(fieldType.Name);

                            if (fieldType.SchemaType is ScalarType s)
                            {
                                joinMethodMap[fieldType.Name] = s.IsNumber ? DataCombineType.Sum : DataCombineType.Assign;
                            }
                        }
                        else
                            primaryNodes.Add(fieldType.Name, fieldType.SchemaType!);
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
                    var origins = await context.GetAppFieldDataAsync(field,oldMap.Values.Concat(nowMap.Values));
                    
                    // Gets the original data
                    Dictionary<string, StructTypeNode> resultMap = new Dictionary<string, StructTypeNode>();
                    if (origins is ArrayTypeNode arr)
                    {
                        foreach (AnySchemaNode token in arr)
                        {
                            if (token is not StructTypeNode obj) continue;
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
                                        if (!bd.Equals(ad))
                                            return ad.CompareTo(bd);
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
        await context.SaveFieldDataAsync(field, result, true);
    }

    // Record the changed fields with changed values
    static void OnFieldDataChanged(SchemaContext context,  AppFieldType field, TransactionChangeOperation operation, AnySchemaNode? value = null, AnySchemaNode? origin = null)
    {
        var transChangedData = context.GetOrCreateContextItem<Dictionary<string, TransactionChangeData>>();
        var access = context.GetSchemaContextItem<Access>();
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
