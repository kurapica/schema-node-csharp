using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Data.Common;
using System.Text.Json.Nodes;
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
                    main = new StructTypeNode((StructType)((StructType)SchemaType).Fields.First(f => f.Name == field.Complex.Main).TypeNode!);
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
    public async Task GenerateDisplayOnlyFields(SchemaContext context, AnySchemaNode? pack)
    {
        // Generate the display only fields
        if (SchemaType is StructType @struct)
        {
            if (pack is StructTypeNode obj)
                await GenerateDisplayOnlyStructFields(context, @struct, obj);
            else if (pack is ArrayTypeNode arr)
            {
                foreach (AnySchemaNode item in arr)
                {
                    if (item is StructTypeNode aObj)
                        await GenerateDisplayOnlyStructFields(context, @struct, aObj);
                }
            }
        }
    }

    #region Utility

    StructTypeNode? ToStructTypeNode(JsonObject? obj)
    {
        if (obj == null || obj.IsEmpty()) return null;
        return (SchemaType is ArrayType array ? array.ElementSchemaType : SchemaType)?.CreateNode(obj) as StructTypeNode;
    }

    // Generate the display only fields
    static async Task GenerateDisplayOnlyStructFields(SchemaContext context, StructType node, StructTypeNode pack)
    {
        if (node.Fields.Length == 0) return;
        foreach (var field in node.Fields)
        {
            if (field.DisplayOnly ?? false)
            {
                var relation = node.Relations?.FirstOrDefault(f => f.Field.Equals(field.Name, StringComparison.OrdinalIgnoreCase) && f.Type == RelationType.Default);
                if (relation == null) continue;

                JsonArray args = new();
                foreach (var arg in relation.Args)
                {
                    args.Add(!string.IsNullOrWhiteSpace(arg.Name) ? pack.GetValueByPaths(arg.Name)?.ToJson() : arg.Value);
                }
                JsonNode? result = await context.CallFunctionAsync(relation.Func, args);
                if (!result.IsEmpty()) pack[field.Name] = result;
            }
            else if (field.TypeNode is StructType @struct && pack.GetField(field.Name) is StructTypeNode spack)
            {
                await GenerateDisplayOnlyStructFields(context, @struct, spack);
            }
            else if (field.TypeNode is ArrayType { ElementSchemaType: StructType arrayStruct } && pack.GetField(field.Name) is ArrayTypeNode { Count: > 0 } arrayPack)
            {
                foreach (var token in arrayPack)
                {
                    if (token is StructTypeNode apack)
                        await GenerateDisplayOnlyStructFields(context, arrayStruct, apack);
                }
            }
            // Fill empty field with default value
            else if (field.TypeNode is ScalarType scalar && !string.IsNullOrWhiteSpace(field.Default) && (pack.GetField(field.Name)?.IsEmpty ?? false))
            {
                (AnySchemaNode? val, JsonNode? err) = await scalar.ValidateValueAsync(context, field.Default);
                if (err == null || err.IsEmpty())
                    pack[field.Name] = val;
            }
        }
    }

    #endregion
}
