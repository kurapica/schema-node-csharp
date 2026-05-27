using SchemaNode.Attribute;
using SchemaNode.Components;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
using ArrayType = SchemaNode.Runtime.ArrayType;
using DecimalType = SchemaNode.Runtime.DecimalType;
using LogicType = SchemaNode.Enum.LogicType;
using StructType = SchemaNode.Runtime.StructType;

namespace SchemaNode.Property.Common;

/// <summary>
/// The data combine rules
/// </summary>
[Meta<Static>]
[Meta<ForSchema>(SCHEMA_KIND_ARRAY)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_COMMON}.{nameof(Combines)}")]
public class Combines : Property<DataCombine[]>;

/// <summary>
/// The data combine settings
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_ARRAY}.combine")]
public sealed record DataCombine(string Field, DataCombineType Type = DataCombineType.Newest);


/// <summary>
/// Combine the data nodes
/// </summary>
internal static class DataCombineTypeExtensions
{
    #region Group Join

    /// <summary>
    /// Join to scalar
    /// </summary>
    internal static DataNode? GroupJoin(DataNode? value, DataCombineType method)
    {
        return method switch
        {
            DataCombineType.Newest => value is ArrayNode arr ? arr.LastOrDefault() : value,
            DataCombineType.Oldest => value is ArrayNode arr ? arr.FirstOrDefault() : value,
            _ => throw new NotImplementedException(),
        };
    }

    /// <summary>
    /// Join to scalar
    /// </summary>
    internal static DataNode? GroupJoin(this ScalarType node, DataNode? value, DataCombineType method)
    {
        return method switch
        {
            DataCombineType.Newest => value is ArrayNode arr ? arr.LastOrDefault() : value,
            DataCombineType.Oldest => value is ArrayNode arr ? arr.FirstOrDefault() : value,
            DataCombineType.Sum => node.From(value is ArrayNode arr ? arr.Sum(a => a.TryGetValue<decimal>(out var d) ? d : 0) : value != null && value.TryGetValue<decimal>(out var s) ? s : 0m),
            DataCombineType.Count => node.From(value is ArrayNode arr ? arr.Count : 0),
            _ => throw new NotImplementedException(),
        };
    }

    /// <summary>
    /// Join to struct
    /// </summary>
    internal static DataNode? GroupJoin(this StructType node, DataNode? value, IReadOnlyDictionary<string, DataCombineType> joinMethodMap)
    {
        if (value == null || value.IsEmpty) return null;
        switch (value)
        {
            case StructNode @struct:
                {
                    // count field
                    foreach ((string field, _) in joinMethodMap.Where(p => p.Value == DataCombineType.Count))
                    {
                        if (node.GetFields().FirstOrDefault(f => f.Name.Equals(field, StringComparison.OrdinalIgnoreCase)) is { Type: Runtime.IntType })
                            @struct[field] = 1;
                    }
                    return @struct;
                }
            case ArrayNode { Count: > 0 } array:
                {
                    // Join
                    StructNode result = new(node);
                    foreach (StructFieldType field in node.GetFields())
                    {
                        switch (joinMethodMap.GetValueOrDefault(field.Name, DataCombineType.Newest))
                        {
                            case DataCombineType.Newest:
                                {
                                    StructNode? last = (StructNode?)array.LastOrDefault(p => p is StructNode obj && !obj.GetAccessValue(field.Name)!.IsEmpty);
                                    if (last != null) result[field.Name] = last[field.Name];
                                    break;
                                }
                            case DataCombineType.Oldest:
                                {
                                    StructNode? first = (StructNode?)array.FirstOrDefault(p => p is StructNode obj && !obj.GetAccessValue(field.Name)!.IsEmpty);
                                    if (first != null) result[field.Name] = first[field.Name];
                                    break;
                                }
                            case DataCombineType.Sum:
                                result[field.Name] = field.Type is DecimalType 
                                    ? array.Sum(p => p is StructNode obj && obj.GetAccessValue(field.Name)?.TryGetValue<decimal>(out var f) == true ? f : 0) 
                                    : field.Type is Runtime.IntType
                                        ? array.Sum(p => p is StructNode obj && obj.GetAccessValue(field.Name)?.TryGetValue<long>(out var f) == true ? f : 0L)
                                        : null;
                                break;
                            case DataCombineType.Count:
                                result[field.Name] = field.Type is Runtime.IntType ? array.Count : null;
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }
                    return value;
                }
        }
        return null;
    }

    /// <summary>
    /// Join to array
    /// </summary>
    internal static Dictionary<string, StructNode> GroupJoinObjectMap(ArrayType node, DataNode? value, Dictionary<string, DataCombineType> joinMethodMap)
    {
        if (value == null || value.IsEmpty) return new();

        // Gets field type
        StructType @struct = (StructType)node.Element!;
        string[] valueFields = (from fieldType in @struct.GetFields() where !node.Primary!.Contains(fieldType.Name) select fieldType.Name).ToArray();

        // The element struct type
        switch (value)
        {
            // Check by value
            case StructNode { IsEmpty: false } o:
                {
                    // Check the primary key
                    string? key = node.GetPrimaryKey(o);
                    if (string.IsNullOrWhiteSpace(key)) return new();

                    // Return single element array
                    return new() { { key, o } };
                }
            case ArrayNode array:
                {
                    // The return list with order
                    Dictionary<string, StructNode> keyMap = new();
                    Dictionary<string, int> keyCount = new();
                    foreach (var token in array)
                    {
                        if (token is not StructNode obj) continue;

                        // Gets the key
                        string? key = node.GetPrimaryKey(obj);
                        if (string.IsNullOrWhiteSpace(key)) continue;
                        if (keyMap.TryGetValue(key, out StructNode? total))
                        {
                            // Join the data fields
                            keyCount[key]++;
                            foreach (string s in valueFields)
                            {
                                switch (joinMethodMap.GetValueOrDefault(s, DataCombineType.Newest))
                                {
                                    case DataCombineType.Newest:
                                        {
                                            if (obj[s] is DataNode { IsEmpty: false } sp)
                                                total[s] = sp;
                                            break;
                                        }

                                    case DataCombineType.Oldest:
                                        if (!(total[s] is DataNode { IsEmpty: false }) && obj[s] is DataNode { IsEmpty: false } c)
                                            total[s] = c;
                                        break;

                                    case DataCombineType.Sum:
                                        total[s] = (total[s] is DataNode { IsEmpty: false } t ? t.ToValue<decimal>() : 0) +
                                                   (obj[s] is DataNode { IsEmpty: false } n ? n.ToValue<decimal>() : 0);
                                        break;

                                    case DataCombineType.Count:
                                        total[s] = (total[s] is DataNode { IsEmpty: false } d ? d.ToValue<int>() : 0) + 1;
                                        break;
                                    default:
                                        throw new ArgumentOutOfRangeException();
                                }
                            }
                        }
                        else
                        {
                            // Add to order list
                            keyMap[key] = obj;
                            keyCount[key] = 1;

                            // Init Count
                            foreach ((string s, DataCombineType m) in joinMethodMap)
                                if (m == DataCombineType.Count)
                                    obj[s] = 1;
                        }
                    }

                    // Gen the result
                    return keyMap;
                }
        }
        return new();
    }

    internal static AppSchemaDataFilter? GetQueryFilter(this StructNode node, ArrayType array)
    {
        if (array.Primary is not { Length: > 0 }) return null;
        AppSchemaDataFilter? filter = null;
        foreach (string primary in array.Primary)
        {
            if (node.GetField(primary) is not DataNode { IsEmpty: false } val) return null;
            var keyFilter = new AppSchemaDataFilterBinary(LogicType.Equal,
                new AppSchemaDataFilterField(primary.ToCamelCase()),
                new AppSchemaDataFilterValue(val));
            filter = filter == null ? keyFilter : filter.AndAlso(keyFilter);
        }
        return filter;

    }
    
    /// <summary>
    /// Join to array
    /// </summary>
    internal static ArrayNode? GroupJoin(this ArrayType node, DataNode? value, Dictionary<string, DataCombineType> joinMethodMap)
    {
        if (node.Element is not StructType structNode || node.Primary == null) return null;
        Dictionary<string, AnySchemaType?> primaryNodes = structNode.Fields.Where(fieldType => node.Primary.Contains(fieldType.Name)).ToDictionary(fieldType => fieldType.Name, fieldType => fieldType.SchemaType);

        // Result
        Dictionary<string, StructNode> resultMap = GroupJoinObjectMap(node, value, joinMethodMap);
        List<StructNode> joinObjs = resultMap.Values.ToList();
        joinObjs.Sort((a, b) =>
        {
            foreach (string s in node.Primary)
            {
                switch (primaryNodes[s])
                {
                    case ScalarType { IsDate: true }:
                        {
                            DateTime ad = a.GetField(s)!.ToValue<DateTime>();
                            DateTime bd = b.GetField(s)!.ToValue<DateTime>();
                            if (!ad.Equals(bd))
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
                            string ad = a[s]?.ToString() ?? string.Empty;
                            string bd = b[s]?.ToString() ?? string.Empty;
                            if (!ad.Equals(bd))
                                return string.Compare(ad, bd, StringComparison.OrdinalIgnoreCase);
                            break;
                        }
                }
            }
            return 0;
        });
        return new ArrayNode(node, joinObjs);
    }

    #endregion
}