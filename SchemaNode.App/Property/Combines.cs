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
    internal static DataNode? GroupJoin(this ScalarType type, DataNode? value, DataCombineType method)
    {
        return method switch
        {
            DataCombineType.Newest => value is ArrayNode arr ? arr.LastOrDefault() : value,
            DataCombineType.Oldest => value is ArrayNode arr ? arr.FirstOrDefault() : value,
            DataCombineType.Sum => type.From(value is ArrayNode arr ? arr.Sum(a => a.TryGetValue<decimal>(out var d) ? d : 0) : value != null && value.TryGetValue<decimal>(out var s) ? s : 0m),
            DataCombineType.Count => type.From(value is ArrayNode arr ? arr.Count : 0),
            _ => throw new NotImplementedException(),
        };
    }

    /// <summary>
    /// Join to struct
    /// </summary>
    internal static DataNode? GroupJoin(this StructType type, DataNode? value, IReadOnlyDictionary<string, DataCombineType> joinMethodMap)
    {
        if (value == null || value.IsEmpty) return null;
        switch (value)
        {
            case StructNode @struct:
                {
                    // count field
                    foreach ((string field, _) in joinMethodMap.Where(p => p.Value == DataCombineType.Count))
                    {
                        if (type.GetFields().FirstOrDefault(f => f.Name.Equals(field, StringComparison.OrdinalIgnoreCase)) is { Type: IntType })
                            @struct[field] = 1;
                    }
                    return @struct;
                }
            case ArrayNode { Count: > 0 } array:
                {
                    // Join
                    StructNode result = new(type);
                    foreach (StructFieldType field in type.GetFields())
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
                                    : field.Type is IntType
                                        ? array.Sum(p => p is StructNode obj && obj.GetAccessValue(field.Name)?.TryGetValue<long>(out var l) == true ? l : 0L)
                                        : null;
                                break;
                            case DataCombineType.Count:
                                result[field.Name] = field.Type is IntType ? array.Count : null;
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }
                    return result;
                }
        }
        return null;
    }

    /// <summary>
    /// Join to array
    /// </summary>
    static Dictionary<string, StructNode> GroupJoinObjectMap(ArrayType type, DataNode? value, Dictionary<string, DataCombineType> joinMethodMap)
    {
        if (value == null || value.IsEmpty) return new Dictionary<string, StructNode>();

        // Gets field type
        StructType @struct = (StructType)type.Element!;
        string[] valueFields = (from fieldType in @struct.GetFields() where !type.Primary!.Contains(fieldType.Name) select fieldType.Name).ToArray();

        // The element struct type
        switch (value)
        {
            // Check by value
            case StructNode { IsEmpty: false } o:
                {
                    // Check the primary key
                    string key = string.Join("|", type.GetPrimaryKeys(o) ?? []);
                    if (string.IsNullOrWhiteSpace(key)) return new Dictionary<string, StructNode>();

                    // Return single element array
                    return new Dictionary<string, StructNode> { { key, o } };
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
                        string key = string.Join("|", type.GetPrimaryKeys(obj) ?? []);
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
                                        total[s] = (total[s] is DataNode { IsEmpty: false } t ? t.GetValue<decimal>() : 0) +
                                                   (obj[s] is DataNode { IsEmpty: false } n ? n.GetValue<decimal>() : 0);
                                        break;

                                    case DataCombineType.Count:
                                        total[s] = (total[s] is DataNode { IsEmpty: false } d ? d.GetValue<int>() : 0) + 1;
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
        return new Dictionary<string, StructNode>();
    }

    internal static AppSchemaDataFilter? GetQueryFilter(this StructNode node, ArrayType array)
    {
        if (array.Primary is not { Count: > 0 }) return null;
        AppSchemaDataFilter? filter = null;
        foreach (string primary in array.Primary)
        {
            if (node.GetAccessValue(primary) is not { IsEmpty: false } val) return null;
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
    internal static ArrayNode? GroupJoin(this ArrayType type, DataNode? value, Dictionary<string, DataCombineType> joinMethodMap)
    {
        if (type.Element is not StructType structNode || type.Primary == null) return null;
        Dictionary<string, Runtime.ValueType?> primaryNodes = structNode.GetFields().Where(fieldType => type.Primary.Contains(fieldType.Name)).ToDictionary(fieldType => fieldType.Name, fieldType => fieldType.Type);

        // Result
        Dictionary<string, StructNode> resultMap = GroupJoinObjectMap(type, value, joinMethodMap);
        List<StructNode> joinObjs = resultMap.Values.ToList();
        joinObjs.Sort((a, b) =>
        {
            foreach (string s in type.Primary)
            {
                switch (primaryNodes[s])
                {
                    case DateType:
                    {
                        var ad = a.GetAccessValue(s)!.GetValue<DateTimeOffset>();
                        var bd = b.GetAccessValue(s)!.GetValue<DateTimeOffset>();
                        if (!ad.Equals(bd)) return ad.CompareTo(bd);
                        break;
                    }
                    case DecimalType:
                    {
                        var ad = a.GetAccessValue(s)!.GetValue<decimal>();
                        var bd = b.GetAccessValue(s)!.GetValue<decimal>();
                        if (ad != bd) return ad < bd ? -1 : 1;
                        break;
                    }
                    case IntType:
                    {
                        var ad = a.GetAccessValue(s)!.GetValue<long>();
                        var bd = b.GetAccessValue(s)!.GetValue<long>();
                        if (ad != bd) return ad < bd ? -1 : 1;
                        break;
                    }
                    default:
                    {
                        string ad = a.GetAccessValue(s)?.GetValue<string>() ?? string.Empty;
                        string bd = b.GetAccessValue(s)?.GetValue<string>() ?? string.Empty;
                        if (!ad.Equals(bd)) return string.Compare(ad, bd, StringComparison.OrdinalIgnoreCase);
                        break;
                    }
                }
            }
            return 0;
        });
        return new ArrayNode(type, joinObjs);
    }

    #endregion
}