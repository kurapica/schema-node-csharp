using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Data.Common;
using System.Text.Json.Nodes;
using System.Transactions;
using SchemaNode.Function;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Components;


/// <summary>
/// The dynamic table structure
/// </summary>
public class DynamicTableSchema
{
    /// <summary>
    /// The sql provider
    /// </summary>
    internal static ISqlProvider SqlProvider = new DefaultSqlProvider();

    /// <summary>
    /// The dynamic table name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The data type name
    /// </summary>
    public required string DataType { get; init; }

    /// <summary>
    /// Whether the table is single row
    /// </summary>
    public bool Single { get; init; }

    /// <summary>
    /// Whether the table use increase update, no full data push allowed
    /// </summary>
    public bool IncrUpdate { get; init; }

    /// <summary>
    /// The dynamic table fields
    /// </summary>
    public IReadOnlyList<DynamicTableField> Fields { get; init; } = [];

    /// <summary>
    /// The dynamic table indexes
    /// </summary>
    public DataIndex[]? Indexes { get; init; } = [];

    /// <summary>
    /// The data type node
    /// </summary>
    public required AnySchemeType SchemaType { get; init; }

    /// <summary>
    /// Gets the field values by the fields
    /// </summary>
    public IEnumerable<(string field, AnySchemaNode? value)> GetFieldValues(JsonObject pack, bool primaryOnly = false, bool noPrimary = false)
    {
        IEnumerable<DynamicTableField> fields = Fields;
        if (primaryOnly) fields = Fields.Where(p => p.Primary);
        else if (noPrimary) fields = Fields.Where(p => !p.Primary);
        foreach (DynamicTableField field in fields)
        {
            if (field.Complex == null)
            {
                if (pack.ContainsKey(field.Name) && !pack[field.Name].IsEmpty())
                {
                    // For save
                    if (field.Type == DynamicTableFieldType.Json)
                    {
                        yield return (field.Name, field.SchemaType.CreateNode(pack[field.Name]));
                    }

                    // For query
                    else if (pack[field.Name] is JsonArray arr && field.SchemaType is not ArrayType)
                    {
                        if (arr.Count > 1)
                        {
                            yield return (field.Name, new ArrayTypeNode(field.SchemaType, arr));
                        }
                        else
                        {
                            yield return (field.Name, field.SchemaType.CreateNode(arr[0]));
                        }
                    }

                    // Single value
                    else
                    {
                        yield return (field.Name, field.SchemaType.CreateNode(pack[field.Name]));
                    }
                }
                else
                {
                    yield return (field.Name, null);
                }
            }
            else if (pack.ContainsKey(field.Complex.Main) && pack[field.Complex.Main] is JsonObject sPack && sPack.ContainsKey(field.Complex.Field) && !sPack[field.Complex.Field].IsEmpty())
            {
                // For save
                if (field.Type == DynamicTableFieldType.Json)
                {
                    yield return (field.Name, field.SchemaType.CreateNode(sPack[field.Complex.Field]));
                }
                // For query
                else if (sPack[field.Complex.Field] is JsonArray arr && field.SchemaType is not ArrayType)
                {
                    if (arr.Count > 1)
                    {
                        yield return (field.Name, new ArrayTypeNode(field.SchemaType, arr));
                    }
                    else
                    {
                        yield return (field.Name, field.SchemaType.CreateNode(arr[0]));
                    }
                }
                else
                // Single value
                {
                    yield return (field.Name, field.SchemaType.CreateNode(sPack[field.Complex.Field]));
                }
            }
            else
            {
                yield return (field.Name, null);
            }
        }
    }

    public IEnumerable<(string field, AnySchemaNode? value)> GetFieldValues(StructTypeNode pack, bool primaryOnly = false, bool noPrimary = false)
    {
        IEnumerable<DynamicTableField> fields = Fields;
        if (primaryOnly) fields = Fields.Where(p => p.Primary);
        else if (noPrimary) fields = Fields.Where(p => !p.Primary);
        foreach (DynamicTableField field in fields)
        {
            if (field.Complex == null)
            {
                AnySchemaNode? fieldNode = pack.GetField(field.Name);
                if (fieldNode is { IsEmpty: false })
                {
                    yield return (field.Name, fieldNode);
                }
                else
                {
                    yield return (field.Name, null);
                }
            }
            else
            {
                AnySchemaNode? complex = pack.GetField(field.Complex.Main);
                if (complex is StructTypeNode sPack && sPack.GetField(field.Complex.Field) is { IsEmpty: false } part)
                {
                    yield return (field.Name, part);
                }
                else
                {
                    yield return (field.Name, null);
                }
            }
        }
    }


    /// <summary>
    /// Gets the primary token from the data
    /// </summary>
    public string? GetPrimaryKey(JsonObject pack)
    {
        StructTypeNode? node = ToStructTypeNode(pack);
        return node != null ? GetPrimaryKey(node) : null;
    }

    /// <summary>
    /// Gets the primary token from the data
    /// </summary>
    public string? GetPrimaryKey(StructTypeNode pack)
    {
        List<string> keys = [];
        foreach ((string _, AnySchemaNode? node) in GetFieldValues(pack, true))
        {
            if (node == null || node.IsEmpty) return null;
            keys.Add(node.ToString());
        }
        return string.Join(":", keys);
    }

    /// <summary>
    /// Gets the field data pack from the reader
    /// </summary>
    public AnySchemaNode? GetFieldPack(DbDataReader reader, int offset = 0)
    {
        // single value
        if (Fields.Count == 1 && Fields[0].SchemaType == SchemaType)
        {
            return Fields[0].FromReader(reader, offset);
        }

        StructTypeNode result = new StructTypeNode((StructType)(SchemaType is ArrayType arr ? arr.ElementSchemaType : SchemaType)!);
        foreach (DynamicTableField field in Fields)
        {
            AnySchemaNode? val = field.FromReader(reader, offset++);
            if (val == null) continue;
            if (field.Complex == null)
            {
                result.SetField(field.Name, val);
            }
            else
            {
                AnySchemaNode? main = result.GetField(field.Complex.Main);
                if (main == null)
                {
                    main = new StructTypeNode((StructType)((StructType)SchemaType).Fields.First(f => f.Name == field.Complex.Main).SchemeType!);
                    result.SetField(field.Complex.Main, main);
                }
                (main as StructTypeNode)![field.Complex.Field] = val;
            }
        }

        return result;
    }

    /// <summary>
    /// Gets the order by fields
    /// </summary>
    public IEnumerable<(string field, bool desc)> GetOrderBys(bool desc = false, AppSchemaDataOrder[]? orderBy = null)
    {
        if (orderBy is { Length: > 0 })
        {
            bool has = false;
            foreach (DynamicTableField field in Fields.Where(f => f.Primary))
            {
                AppSchemaDataOrder? order = orderBy.FirstOrDefault(o => o.Field.Equals(field.Name, StringComparison.OrdinalIgnoreCase));
                if (order == null) continue;

                has = true;
                yield return (field.Name, order.Desc);
            }
            if (has) yield break;
        }
        yield return (DYNAMIC_TABLE_SEQNO_FIELD, desc);
    }

    /// <summary>
    /// Generate display only fields
    /// </summary>
    public Task GenerateDisplayOnlyFields(SchemaContext context, AnySchemaNode? pack)
    {
        // Generate the display only fields
        return SchemaType is StructType @struct 
            ? GenerateDisplayOnlyFields(context, @struct, pack)
            : Task.CompletedTask;
    }

    #region Utility

    private static readonly string[] JoinFuncs =
    [
        $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappfdata)}",
        $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappfdatabyonekey)}",
        $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappfdatabytwokey)}",
        $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappfdatabythreekey)}",
        $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappfdatabyfourkey)}",
    ];

    private StructTypeNode? ToStructTypeNode(JsonObject? obj)
    {
        if (obj == null || obj.IsEmpty()) return null;
        return (SchemaType is ArrayType array ? array.ElementSchemaType : SchemaType)?.CreateNode(obj) as StructTypeNode;
    }

    // Generate the display only fields
    private static async Task GenerateDisplayOnlyFields(SchemaContext context, StructType type, AnySchemaNode? node, bool joinHandled = false)
    {
        if (type.Fields.Length == 0) return;
        switch (node)
        {
            case ArrayTypeNode array:
            {
                // batch process for join functions
                if (type.Relations != null)
                {
                    foreach (StructFieldRelation relation in (type.Relations.Where(r =>
                                 r.Type == RelationType.Default && JoinFuncs.Contains(r.Func) &&
                                 type.GetField(r.Field) != null && (type.GetField(r.Field)?.DisplayOnly ?? false))))
                    {
                        // app
                        string? app = relation.Args.FirstOrDefault()?.Value?.ToValue<string>();
                        if (string.IsNullOrWhiteSpace(app)) continue; // no app
                        AppType? appType = await context.GetAppTypeAsync(app);
                        if (appType == null) continue; // app not exist

                        // app field
                        string? field = relation.Args.ElementAtOrDefault(1)?.Value?.ToValue<string>();
                        if (string.IsNullOrWhiteSpace(field)) continue; // no app field
                        AppFieldType? appField = appType.GetField(field);
                        if (appField == null) continue; // app field not exist

                        // primary & struct
                        string[] primary = (appField.SchemaType as ArrayType)?.Primary ?? [];
                        StructType? structType =
                            (appField.SchemaType is ArrayType arr
                                ? arr.ElementSchemaType
                                : appField.SchemaType) as StructType;
                        if (structType == null || structType.Fields.Length == 0) continue;
                        if (primary.Length + 4 != relation.Args.Length) continue; // primary fields not match

                        // data field
                        string? dataField = relation.Args.ElementAtOrDefault(2)?.Value?.ToValue<string>();
                        if (string.IsNullOrWhiteSpace(dataField)) continue; // no data field
                        StructFieldConfig? dataFieldType = structType.GetField(dataField);
                        if (dataFieldType == null) continue; // data field not exist

                        // target
                        var targetArg = relation.Args.Last();
                        string? target = targetArg.Value?.ToValue<string>() ??
                                         context.GetSchemaContextItem<Access>()?.Target;
                        if (string.IsNullOrWhiteSpace(target)) continue; // no app target

                        // collect keys
                        Dictionary<string, List<AnySchemaNode>> keyMap = new();
                        JsonArray queries = [];
                        if (primary.Length > 0)
                        {
                            foreach (AnySchemaNode row in array)
                            {
                                if (row is not StructTypeNode pack) continue;

                                // build primary key
                                List<string> keys = [];
                                JsonObject query = [];
                                for (int i = 3; i < relation.Args.Length - 1; i++)
                                {
                                    string? key = !string.IsNullOrEmpty(relation.Args[i].Name)
                                        ? pack.GetValueByPaths(relation.Args[i].Name!)?.ToString()
                                        : relation.Args[i].Value?.ToValue<string>();
                                    
                                    if (string.IsNullOrEmpty(key))
                                    {
                                        keys.Clear();
                                        break;
                                    }
                                    query[primary[i - 3]] = key;
                                    keys.Add(key);
                                }
                                
                                if (keys.Count == 0) continue; // no valid primary key
                                string pkey = string.Join(":", keys);

                                // add to map
                                if (!keyMap.ContainsKey(pkey))
                                {
                                    keyMap[pkey] = [];
                                    queries.Add(query);
                                }

                                keyMap[pkey].Add(pack);
                            }
                        }

                        // query the dynamic data
                        (AnySchemaNode? value, _) = await context.GetFieldDataAsync(appField, target, queries);

                        // set the display only field value
                        switch (primary.Length)
                        {
                            case > 0 when value is ArrayTypeNode resultArray:
                            {
                                foreach (AnySchemaNode resultRow in resultArray)
                                {
                                    if (resultRow is not StructTypeNode resultStruct) continue;

                                    // build primary key
                                    List<string> keys = [];
                                    foreach (string path in primary)
                                    {
                                        AnySchemaNode? n = resultStruct.GetValueByPaths(path);
                                        if (n == null || n.IsEmpty)
                                        {
                                            keys.Clear();
                                            break;
                                        }

                                        keys.Add(n.ToString());
                                    }

                                    if (keys.Count == 0) continue; // no valid primary key
                                    string pkey = string.Join(":", keys);

                                    // get data node
                                    AnySchemaNode? dataNode = resultStruct.GetValueByPaths(dataField);
                                    if (dataNode == null || dataNode.IsEmpty) continue;

                                    // set value
                                    if (!keyMap.TryGetValue(pkey, out List<AnySchemaNode>? packs)) continue;
                                    foreach (AnySchemaNode row in packs)
                                    {
                                        if (row is not StructTypeNode pack) continue;
                                        AnySchemaNode? fld = pack.GetField(relation.Field);
                                        if (fld is not { IsEmpty: true }) continue;

                                        // set value
                                        fld.Value = dataNode;
                                    }
                                }

                                break;
                            }
                            case 0 when value is StructTypeNode resultStruct:
                            {
                                // single key
                                AnySchemaNode? dataNode = resultStruct.GetValueByPaths(dataField);
                                if (dataNode == null || dataNode.IsEmpty) continue;

                                foreach (AnySchemaNode row in array)
                                {
                                    if (row is not StructTypeNode pack) continue;
                                    AnySchemaNode? fld = pack.GetField(relation.Field);
                                    if (fld is not { IsEmpty: true }) continue;

                                    // set value
                                    fld.Value = dataNode;
                                }

                                break;
                            }
                        }
                    }
                }
                
                // generate for each row
                foreach (AnySchemaNode row in array)
                    await GenerateDisplayOnlyFields(context, type, row, true);
                break;
            }
            case StructTypeNode pack:
            {
                foreach (var field in type.Fields)
                {
                    // Gets the field node
                    AnySchemaNode? fld = pack.GetField(field.Name);
                    if (fld == null) continue; // impossible
                    
                    if (field.DisplayOnly ?? false)
                    {
                        if (!fld.IsEmpty) continue; // already set value
                
                        // default for display only
                        var relation = type.Relations?.FirstOrDefault(f => f.Field.Equals(field.Name, StringComparison.OrdinalIgnoreCase) && f.Type == RelationType.Default);
                        if (relation == null) continue;
                        
                        // handled by array node
                        if (joinHandled && JoinFuncs.Contains(relation.Func)) continue; 

                        // call function to get value
                        JsonArray args = [];
                        foreach (var arg in relation.Args)
                            args.Add(!string.IsNullOrWhiteSpace(arg.Name) ? pack.GetValueByPaths(arg.Name)?.ToJson() : arg.Value?.DeepClone());
                        JsonNode? result = await context.CallFunctionAsync(relation.Func, args, [fld.SchemeType.Name]);
                        if (!result.IsEmpty()) fld.Value = result;
                    }
                    else switch (field.SchemeType)
                    {
                        case StructType @struct:
                            await GenerateDisplayOnlyFields(context, @struct, fld);
                            break;
                        case ArrayType { ElementSchemaType: StructType arrayStruct }:
                            await GenerateDisplayOnlyFields(context, arrayStruct, fld);
                            break;
                        // Fill empty field with default value
                        default:
                            if (fld is ScalarTypeNode or EnumTypeNode && fld.IsEmpty && !string.IsNullOrWhiteSpace(field.Default))
                            {
                                (AnySchemaNode? val, JsonNode? err) = await fld.SchemeType.ValidateValueAsync(context, field.Default);
                                if (err == null || err.IsEmpty())
                                    fld.Value = val;
                            }
                            break;
                    }
                }
                
                break;
            }
        }
    }

    #endregion
}
