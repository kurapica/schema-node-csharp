using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Data;
using SchemaNode.Enum;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;
using SchemaKind = SchemaNode.Property.Record.SchemaKind;
using SchemaNode.Property.Core;
using SchemaNode.Scalar;
using SchemaNode.Property.Common;
using SchemaNode.Function;
using SchemaNode.Node;
using SchemaNode.Property.Constraint;
using SchemaNode.Runtime;
using SchemaNode.Utility;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Schema;

/// <summary>
/// The application field schema
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP_FIELD}.schema")]
[Meta<SchemaKind>(SCHEMA_KIND_APP_FIELD, SCHEMA_KIND_ORDER_APP_FIELD)]
[Meta<Append>(typeof(Display), typeof(Disable))]
public sealed class AppFieldSchema: ExtensibleSchema
{
    #region Base
    
    /// <summary>
    /// the application name
    /// </summary>
    [Meta<PrimaryIndex>(0)]
    [Meta<SchemaType>(typeof(AppType))]
    public string App { get; set; } = string.Empty;

    /// <summary>
    /// The field name
    /// </summary>
    [Meta<PrimaryIndex>(1)]
    [Meta<SchemaType>(typeof(Identifier))]
    public string Name { get; set; } = default!;

    /// <summary>
    /// The seqno
    /// </summary>
    [SchemaIgnore]
    public int Seqno { get; set; }

    /// <summary>
    /// The field type
    /// </summary>
    [Meta<SchemaType>(typeof(ValueType))]
    public string Type { get; set; } = default!;
    
    #endregion
    
    #region Source Push
    
    /// <summary>
    /// The input source field
    /// </summary>
    [Meta<SchemaType>(typeof(Identifier))]
    [Meta<EntrySource>($"{NS_SYSTEM_SCHEMA_REFLECT}.{nameof(SystemAppReflect.getappfields)}", $"${nameof(App)}")]
    public string? Source { get; set; }
    
    [Meta<SchemaType>(typeof(ValueType))]
    [Meta<DisplayOnly>(true)]
    [Meta<InVisible>(true)]
    [Relation<Default>($"{NS_SYSTEM_SCHEMA_REFLECT}.{nameof(SystemAppReflect.getappfieldtype)}",  $"${nameof(App)}", $"${nameof(Source)}", true)]
    public string? SourceType { get; set; }

    /// <summary>
    /// The push function, convert the input data to the type data
    /// </summary>
    [Meta<SchemaType>(typeof(FuncType))]
    [Relation<Visible>($"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.notempty)}", $"${nameof(Source)}")]
    [Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_FUNC_WITH_ARGS, NODE_SELF, $"${nameof(SourceType)}")]
    [Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_FUNC_WITH_RETURN, NODE_SELF, $"${nameof(Type)}", true)]
    public string? Push { get; set; }
    
    /// <summary>
    /// The combine rule for scalar/enum type
    /// </summary>
    [Relation<InVisible>($"${NS_SYSTEM_SCHEMA_REFLECT}.{nameof(SystemReflect.isschemakind)}", $"${nameof(Type)}", SCHEMA_KIND_STRUCT, true)]
    public DataCombineType? Combine { get; set; }
    
    /// <summary>
    /// The combine rule for struct or struct-array type
    /// </summary>
    [Relation<Visible>($"${NS_SYSTEM_SCHEMA_REFLECT}.{nameof(SystemReflect.isschemakind)}", $"${nameof(Type)}", SCHEMA_KIND_STRUCT, true)]
    public DataCombine[]? Combines { get; set; }

    #endregion
    
    #region Foreign & View
    
    /// <summary>
    /// The foreign key settings
    /// </summary>
    public Foreign[]? Foreigns { get; set; }

    /// <summary>
    /// The field view settings
    /// </summary>
    public FieldView? View { get; set; }

    #endregion
}

#region Help Types

/// <summary>
/// The foreign settings
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP_FIELD}.foreign")]
public sealed class Foreign
{
    /// <summary>
    /// The foreign app name
    /// </summary>
    [Meta<SchemaType>(typeof(AppType))]
    public string App { get; set; } = string.Empty;
    
    /// <summary>
    /// The field refer to the other app target
    /// </summary>
    [Meta<EntrySource>($"{NS_SYSTEM_SCHEMA_REFLECT}.{nameof(SystemAppReflect.getappfields)}", $"${nameof(App)}")]
    public string Field { get; set; } = string.Empty;
    
    [JsonIgnore]
    [SchemaIgnore]
    public Runtime.AppType? AppType { get; set; }
}

[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP_FIELD}.view")]
public sealed class FieldView
{
    /// <summary>
    /// The source application
    /// </summary>
    [Meta<SchemaType>(typeof(AppType))]
    public string App { get; set; } = string.Empty;

    /// <summary>
    /// The source field
    /// </summary>
    [Meta<EntrySource>($"{NS_SYSTEM_SCHEMA_REFLECT}.{nameof(SystemAppReflect.getappfields)}", $"${nameof(App)}")]
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// The target map field
    /// </summary>
    public string Map { get; set; } = string.Empty;

    [SchemaIgnore]
    [JsonIgnore]
    public Runtime.AppType? AppType { get; set; }
}

/// <summary>
/// The data combine settings
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP_FIELD}.combine")]
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
    internal static DataNode? GroupJoin(this Runtime.StructType type, DataNode? value, IReadOnlyDictionary<string, DataCombineType> joinMethodMap)
    {
        if (value == null || value.IsEmpty) return null;
        switch (value)
        {
            case StructNode @struct:
                {
                    // count field
                    foreach ((string field, _) in joinMethodMap.Where(p => p.Value == DataCombineType.Count))
                    {
                        if (type.GetFields().FirstOrDefault(f => f.Name.Equals(field, StringComparison.OrdinalIgnoreCase)) is { Type: Runtime.IntType })
                            @struct.TrySetFieldValue(field, 1);
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
                                if (field.Type is Runtime.DecimalType)
                                    result.TrySetFieldValue(field.Name, array.Sum(p => p is StructNode obj && obj.GetAccessValue(field.Name)?.TryGetValue<decimal>(out var f) == true ? f : 0));
                                else if (field.Type is Runtime.IntType)
                                    result.TrySetFieldValue(field.Name, array.Sum(p => p is StructNode obj && obj.GetAccessValue(field.Name)?.TryGetValue<long>(out var l) == true ? l : 0L));
                                break;
                            case DataCombineType.Count:
                                if (field.Type is Runtime.IntType)
                                    result.TrySetFieldValue(field.Name, array.Count);
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
    internal static Dictionary<string, StructNode> GroupJoinObjectMap(Runtime.ArrayType type, DataNode? value, Dictionary<string, DataCombineType> joinMethodMap)
    {
        if (value == null || value.IsEmpty) return new Dictionary<string, StructNode>();

        // Gets field type
        var @struct = (Runtime.StructType)type.Element!;
        string[] valueFields = (from fieldType in @struct.GetFields() where !type.Primary!.Contains(fieldType.Name) select fieldType.Name).ToArray();

        // The element struct type
        switch (value)
        {
            // Check by value
            case StructNode { IsEmpty: false } o:
                {
                    // Check the primary key
                    string? key = type.GetPrimaryKey(o);
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
                        string? key = type.GetPrimaryKey(obj);
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
                                        total.TrySetFieldValue(s, (total[s] is DataNode { IsEmpty: false } t ? t.GetValue<decimal>() : 0) +
                                                              (obj[s] is DataNode { IsEmpty: false } n ? n.GetValue<decimal>() : 0));
                                        break;

                                    case DataCombineType.Count:
                                        total.TrySetFieldValue(s, (total[s] is DataNode { IsEmpty: false } d ? d.GetValue<int>() : 0) + 1);
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
                                    obj.TrySetFieldValue(s, 1);
                        }
                    }

                    // Gen the result
                    return keyMap;
                }
        }
        return new Dictionary<string, StructNode>();
    }

    internal static AppSchemaDataFilter? GetQueryFilter(this StructNode node, Runtime.ArrayType array)
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
    internal static ArrayNode? GroupJoin(this Runtime.ArrayType type, DataNode? value, Dictionary<string, DataCombineType> joinMethodMap)
    {
        if (type.Element is not Runtime.StructType structNode || type.Primary == null) return null;
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
                    case Runtime.DateType:
                    {
                        var ad = a.GetAccessValue(s)!.GetValue<DateTimeOffset>();
                        var bd = b.GetAccessValue(s)!.GetValue<DateTimeOffset>();
                        if (!ad.Equals(bd)) return ad.CompareTo(bd);
                        break;
                    }
                    case Runtime.DecimalType:
                    {
                        var ad = a.GetAccessValue(s)!.GetValue<decimal>();
                        var bd = b.GetAccessValue(s)!.GetValue<decimal>();
                        if (ad != bd) return ad < bd ? -1 : 1;
                        break;
                    }
                    case Runtime.IntType:
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

#endregion
