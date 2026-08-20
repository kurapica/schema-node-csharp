using System.Text.Json.Nodes;
using SchemaNode.Attribute;
using SchemaNode.Data;
using SchemaNode.Enum;
using SchemaNode.Function;
using SchemaNode.Node;
using SchemaNode.Property.Common;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Scalar;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;
using SchemaValueType = SchemaNode.Schema.ValueType;
using SchemaNode.Relation;

namespace SchemaNode.Property.App;

/// <summary>
/// The data derive settings
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_APP_FIELD)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_APP}.{nameof(DataDerive)}")]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<Static>(true)]
[Relation<Visible, Call>(nameof(DataDerive), $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(EnableStorage)}")]
[Relation<EntrySource, Assign>($"{nameof(DataDerive)}.{nameof(Derive.Source)}", $"{NS_SYSTEM_SCHEMA_REFLECT_APP}.{nameof(SystemReflectApp.getappfields)}", $"@{nameof(AppFieldSchema.App)}")]
[Relation<BlackList, Call>($"{nameof(DataDerive)}.{nameof(Derive.Source)}", $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.newarray)}", $"@{nameof(AppFieldSchema.Name)}")]
[Relation<Default, Call>($"{nameof(DataDerive)}.{nameof(Derive.SourceType)}", $"{NS_SYSTEM_SCHEMA_REFLECT_APP}.{nameof(SystemReflectApp.getappfieldtype)}",  $"@{nameof(App)}", $"@{nameof(DataDerive)}.{nameof(Derive.Source)}", true)]
[Relation<Default, Call>($"{nameof(DataDerive)}.{nameof(Derive.FieldType)}", $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}",  $"@{nameof(AppFieldSchema.Type)}")]
public class DataDerive : Property<Derive>
{
    public override void SetValue<TValue>(TValue value)
    {
        if (value is Derive or JsonObject)
        {
            base.SetValue(value);
            return;
        }

        // for code declare
        string calc = string.Empty;
        string source = string.Empty;
        if (value is object[] objs)
        {
            calc = objs.ElementAtOrDefault(0)?.ToString() ?? string.Empty;
            source = objs.ElementAtOrDefault(1)?.ToString() ?? string.Empty;
        }

        if (!string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(calc))
            base.SetValue(new Derive { Source = source, Calc =  calc });
    }
}

/// <summary>
/// The data derive settings
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP_FIELD}.{nameof(Derive)}")]
public class Derive
{
    /// <summary>
    /// The input source field
    /// </summary>
    [Meta<SchemaType>(typeof(Identifier))]
    [Meta<Require>(true)]
    public string Source { get; set; } = string.Empty;
    
    /// <summary>
    /// The field type
    /// </summary>
    [Meta<SchemaType>(typeof(SchemaValueType))]
    [Meta<DisplayOnly>(true)]
    [Meta<InVisible>(true)]
    public string? FieldType { get; set; }

    /// <summary>
    /// The source type
    /// </summary>
    [Meta<SchemaType>(typeof(SchemaValueType))]
    [Meta<DisplayOnly>(true)]
    [Meta<InVisible>(true)]
    public string? SourceType { get; set; }

    /// <summary>
    /// The calculate function, convert the input data to the type data
    /// </summary>
    [Meta<SchemaType>(typeof(FuncType))]
    [Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_FUNC_WITH_ARGS, NODE_SELF, $"@{nameof(SourceType)}")]
    [Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_FUNC_WITH_RETURN, NODE_SELF, $"@{nameof(FieldType)}", true)]
    [Meta<Require>(true)]
    public string Calc { get; set; } = string.Empty;
    
    /// <summary>
    /// The combine rule for scalar/enum type
    /// </summary>
    [Relation<Visible, Relation.Call>(NODE_SELF, $"{NS_SYSTEM_SCHEMA_REFLECT_IS_SCHEMA_KIND}", $"@{nameof(FieldType)}", true, SCHEMA_KIND_ENUM, SCHEMA_KIND_BOOL, SCHEMA_KIND_STRING, SCHEMA_KIND_INT, SCHEMA_KIND_DECIMAL, SCHEMA_KIND_DATE)]
    [Relation<WhiteList, Relation.Call>(NODE_SELF, $"{NS_SYSTEM_SCHEMA_REFLECT_APP}.{nameof(SystemReflectApp.getcombinetype)}", $"@{nameof(FieldType)}")]
    public DataCombineType? Combine { get; set; }
    
    /// <summary>
    /// The combine rule for struct or struct-array type
    /// </summary>
    [Relation<Visible, Relation.Call>(NODE_SELF, $"{NS_SYSTEM_SCHEMA_REFLECT_IS_SCHEMA_KIND}", $"@{nameof(FieldType)}", true, SCHEMA_KIND_STRUCT)]
    [Relation<EntrySource, Relation.Assign>($"{nameof(Combines)}.{ARRAY_ELEMENT}.{nameof(FieldCombine.Field)}", $"{NS_SYSTEM_SCHEMA_REFLECT_APP}.{nameof(SystemReflectApp.getcombinefields)}", $"@{nameof(FieldType)}")]
    [Relation<BlackList, Relation.Call>($"{nameof(Combines)}.{ARRAY_ELEMENT}.{nameof(FieldCombine.Field)}", $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.getfields)}", $"@{nameof(Combines)}.{ARRAY_PREVIOUS}", $"{nameof(FieldCombine.Field)}")]
    [Relation<Default, Relation.Call>($"{nameof(Combines)}.{ARRAY_ELEMENT}.{nameof(FieldCombine.FieldType)}", $"{NS_SYSTEM_SCHEMA_REFLECT_TYPE}.{nameof(SchemaNode.Function.Reflect.Type.getaccesstype)}", $"@{nameof(FieldType)}", $"@{nameof(Combines)}.{ARRAY_ELEMENT}.{nameof(FieldCombine.Field)}")]
    public FieldCombine[]? Combines { get; set; }
}


/// <summary>
/// The data combine settings
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP_FIELD}.combine")]
public sealed class FieldCombine
{
    /// <summary>
    /// The field name to combine
    /// </summary>
    [Meta<PrimaryIndex>][Meta<SchemaType>(typeof(Identifier))]
    public required string Field { get; set; }

    /// <summary>
    /// The field type
    /// </summary>
    [Meta<SchemaType>(typeof(SchemaValueType))] 
    [Meta<DisplayOnly>(true)]
    [Meta<InVisible>(true)]
    public string? FieldType { get; set; }

    /// <summary>
    /// The combine rule
    /// </summary>
    [Relation<WhiteList, Call>(NODE_SELF, $"{NS_SYSTEM_SCHEMA_REFLECT_APP}.{nameof(SystemReflectApp.getcombinetype)}", $"@{nameof(FieldType)}")]
    public DataCombineType? Type { get; set; }
}

/// <summary>
/// CombineProperties the data nodes
/// </summary>
internal static class DataDeriveExtensions
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
                                if (field.Type is Runtime.DecimalType)
                                    result[field.Name] = array.Sum(p => p is StructNode obj && obj.GetAccessValue(field.Name)?.TryGetValue<decimal>(out var f) == true ? f : 0);
                                else if (field.Type is Runtime.IntType)
                                    result[field.Name] = array.Sum(p => p is StructNode obj && obj.GetAccessValue(field.Name)?.TryGetValue<long>(out var l) == true ? l : 0L);
                                break;
                            case DataCombineType.Count:
                                if (field.Type is Runtime.IntType)
                                    result[field.Name] = array.Count;
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
