using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Data.Common;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using SchemaNode.Components.Provider;
using static SchemaNode.Utility.Constant;
using SchemaNode.Node;
using SchemaNode.Runtime;
// ReSharper disable UnusedAutoPropertyAccessor.Global

/// <summary>
/// The in-memory application field schema representation
/// </summary>
public class AppFieldType
{
    #region Properties

    /// <summary>
    /// The application name
    /// </summary>
    public string App { get; internal set; } = string.Empty;
    
    /// <summary>
    /// The seqno
    /// </summary>
    public int Seqno { get; internal set; }

    /// <summary>
    /// The field name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The field type.
    /// </summary>
    public string Type { get; init; } = default!;

    /// <summary>
    /// The field chinese name.
    /// </summary>
    public LocaleString? Display { get; init; }

    /// <summary>
    /// The description of the field.
    /// </summary>
    public LocaleString? Desc { get; init; }

    /// <summary>
    /// The source application
    /// </summary>
    public string? SourceApp { get; init; }

    /// <summary>
    /// The source field
    /// </summary>
    public string? SourceField { get; init; }

    /// <summary>
    /// Track the push data to the source field, so toggle the source target, will also re-push the data
    /// </summary>
    public bool? TrackPush { get; init; }
    
    /// <summary>
    /// The calculate function
    /// </summary>
    public string? Func { get; init; }

    /// <summary>
    /// The input fields
    /// </summary>
    public string[]? Args { get; init; }

    /// <summary>
    /// The field is using increase update, no full data push allowed
    /// </summary>
    public bool? IncrUpdate { get; init; }

    /// <summary>
    /// The field is front-end only, no data storage
    /// </summary>
    public bool? Frontend { get; init; }

    /// <summary>
    /// The field is disabled
    /// </summary>
    public bool? Disable { get; init; }
    
    /// <summary>
    /// The field is readonly
    /// </summary>
    public bool? Readonly { get; init; }

    /// <summary>
    /// The combine rule for scalar/enum type
    /// </summary>
    public DataCombineType? Combine { get; init; }

    /// <summary>
    /// The combine rule for struct or struct-array type
    /// </summary>
    public DataCombine[]? Combines { get; init; }

    /// <summary>
    /// The additional data
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; init; }

    #endregion

    #region States

    /// <summary>
    /// The application field node status
    /// </summary>
    public SchemaNodeStatus Status { get; internal set; } = SchemaNodeStatus.Ready;

    /// <summary>
    /// Enable dynamic table
    /// </summary>
    public bool EnableDynamicTable => !(Frontend ?? false) && !(Disable ?? false) && (SourceFieldType == null || TrackPush == true && FuncNode != null);

    /// <summary>
    /// Enable push track table
    /// </summary>
    public bool EnablePushTrackTable => SourceFieldType != null && EnableDynamicTable;
    
    /// <summary>
    /// The data is queryable
    /// </summary>
    public bool IsQueryable => !(Frontend ?? false) && !(Disable ?? false) && (SourceFieldType == null || FuncNode == null);

    /// <summary>
    /// Has observers
    /// </summary>
    public bool HasObserver => _observers is { Count: > 0 };

    #endregion

    #region Relationship

    /// <summary>
    /// The application node
    /// </summary>
    public AppType Application { get; internal set; } = default!;
    
    /// <summary>
    /// The field type node
    /// </summary>
    public AnySchemeType? SchemaType { get; internal set; }

    /// <summary>
    /// The field function node
    /// </summary>
    public FunctionType? FuncNode { get; internal set; }

    /// <summary>
    /// The fields that subscribe the update of this field.
    /// </summary>
    public IReadOnlyList<AppFieldType>? Observers => _observers;
    
    /// <summary>
    ///  the observers in the same app
    /// </summary>
    List<AppFieldType>? _observers;

    /// <summary>
    /// The call arguments
    /// </summary>
    public List<AppFieldNodeArgument>? FuncArgs { get; internal set; }

    /// <summary>
    /// The source app type, won't be reloaded
    /// </summary>
    public AppType? SourceAppType { get; internal set; }

    /// <summary>
    /// The source node
    /// </summary>
    public AppFieldType? SourceFieldType => SourceAppType?.GetField(SourceField);
    
    /// <summary>
    /// The dynamic table schema
    /// </summary>
    public DynamicTableSchema? Schema { get; internal set; }

    #endregion
    
    #region Method
    
    /// <summary>
    /// Add observer
    /// </summary>
    public void AddObserver(AppFieldType observer)
    {
        _observers ??= new List<AppFieldType>();
        if (!_observers.Contains(observer))
            _observers.Add(observer);
    }
    
    #endregion

    #region Conversions

    /// <summary>
    /// Gets the field node from entity
    /// </summary>
    public static implicit operator AppFieldType(AppFieldSchema entity)
    {
        return new AppFieldType
        {
            Name = entity.Name,
            Type = entity.Type,
            Seqno = entity.Seqno,
            Display = entity.Display,
            Desc = entity.Desc,
            SourceApp = entity.SourceApp,
            SourceField = entity.SourceField,
            TrackPush = entity.TrackPush,
            Func = entity.Func,
            Args = entity.Args,
            IncrUpdate = entity.IncrUpdate,
            Frontend = entity.Frontend,
            Disable = entity.Disable,
            Readonly = entity.Readonly,
            Combine = entity.Combine,
            Combines = entity.Combines,
            Additional = entity.Additional,
        };
    }

    /// <summary>
    /// Convert the node to schema
    /// </summary>
    public static implicit operator AppFieldSchema(AppFieldType entity)
    {
        return new AppFieldSchema
        {
            App = entity.App,
            Name = entity.Name,
            Type = entity.Type,
            Seqno = entity.Seqno,
            Display = entity.Display,
            Desc = entity.Desc,
            SourceApp = entity.SourceApp,
            SourceField = entity.SourceField,
            TrackPush = entity.TrackPush,
            Func = entity.Func,
            Args = entity.Args,
            IncrUpdate = entity.IncrUpdate,
            Frontend = entity.Frontend,
            Disable = entity.Disable,
            Readonly = entity.Readonly,
            Combine = entity.Combine,
            Combines = entity.Combines,
            Additional = entity.Additional,
        };
    }

    #endregion

    #region Dynamic table

    // Gets the data field dynamic table name
    public string DynamicTableName => $"{DYNAMIC_TABLE_PREFIX}_{Regex.Replace(App, @"\W+", "_")}_{Name}";

    // Generate the dynamic table schema
    public DynamicTableSchema GenDynamicTableSchema()
    {
        // Generate the fields
        AnySchemeType node = SchemaType!;
        List<DynamicTableField> fields = new();
        DataIndex[]? indexes = null;
        bool single = true;
        DataTypeInfo info;

        if (Frontend ?? false)
        {
            return new DynamicTableSchema
            {
                Name = DynamicTableName,
                DataType = Type,
                SchemaType = node,
                Single = true,
                Fields = fields,
            };
        }

        switch (node.Type)
        {
            case SchemaNode.Enum.SchemaType.Scalar:
            case SchemaNode.Enum.SchemaType.Enum:
            case SchemaNode.Enum.SchemaType.Json:
                {
                    info = GetDataTypeInfo(node);
                    fields.Add(new DynamicTableField
                    {
                        Name = DYNAMIC_TABLE_VALUE_FIELD,
                        Type = info.Type,
                        MaxLength = info.MaxLength,
                        SchemaType = node
                    });
                    break;
                }
            case SchemaNode.Enum.SchemaType.Struct:
                {
                    StructType structNode = (StructType)node;
                    foreach (var sField in structNode.Fields.Where(p => !(p.DisplayOnly ?? false)))
                    {
                        if (sField.TypeNode?.Type == SchemaNode.Enum.SchemaType.Struct) // Check if the sfield use a struct type
                        {
                            // As complex fields
                            StructType subStructNode = (StructType)sField.TypeNode;
                            foreach (var iField in subStructNode.Fields.Where(p => !(p.DisplayOnly ?? false)))
                            {
                                info = GetDataTypeInfo(iField.TypeNode!, iField);
                                fields.Add(new DynamicTableField
                                {
                                    Name = $"{sField.Name}{COMPLEX_SEP}{iField.Name}",
                                    Complex = new DataFieldComplexInfo
                                    {
                                        Main = sField.Name,
                                        Field = iField.Name
                                    },
                                    Type = info.Type,
                                    MaxLength = info.MaxLength,
                                    SchemaType = iField.TypeNode!
                                });
                            }
                        }
                        else
                        {
                            info = GetDataTypeInfo(sField.TypeNode!, sField);
                            fields.Add(new DynamicTableField
                            {
                                Name = sField.Name,
                                Type = info.Type,
                                MaxLength = info.MaxLength,
                                SchemaType = sField.TypeNode!
                            });
                        }
                    }
                    break;
                }
            case SchemaNode.Enum.SchemaType.Array:
                {
                    ArrayType arrayNode = (ArrayType)node;
                    node = arrayNode.ElementSchemaType!; // Record the base node for array
                    indexes = arrayNode.Indexes;
                    if (node is StructType structNode && arrayNode.Primary is { Length: > 0 })
                    {
                        single = false;

                        // Add primary fields
                        foreach (string n in arrayNode.Primary)
                        {
                            var sField = structNode.Fields.First(p => p.Name == n);
                            info = GetDataTypeInfo(sField.TypeNode!, sField);
                            fields.Add(new DynamicTableField
                            {
                                Name = sField.Name,
                                Type = info.Type,
                                Primary = true,
                                MaxLength = info.MaxLength,
                                SchemaType = sField.TypeNode!,
                                StructFieldNode = sField,
                            });
                        }
                        // Add normal fields
                        foreach (var sField in structNode.Fields.Where(p => !arrayNode.Primary.Contains(p.Name) && !(p.DisplayOnly ?? false)))
                        {
                            // Check if the sfield use a struct type
                            if (sField.TypeNode!.Type == SchemaNode.Enum.SchemaType.Struct)
                            {
                                // As complex fields
                                foreach (var ifield in ((StructType)sField.TypeNode).Fields.Where(p => !(p.DisplayOnly ?? false)))
                                {
                                    info = GetDataTypeInfo(ifield.TypeNode!, ifield);
                                    fields.Add(new DynamicTableField
                                    {
                                        Name = $"{sField.Name}{COMPLEX_SEP}{ifield.Name}",
                                        Complex = new DataFieldComplexInfo
                                        {
                                            Main = sField.Name,
                                            Field = ifield.Name
                                        },
                                        Type = info.Type,
                                        MaxLength = info.MaxLength,
                                        SchemaType = ifield.TypeNode!
                                    });
                                }
                            }
                            else
                            {
                                info = GetDataTypeInfo(sField.TypeNode, sField);
                                fields.Add(new DynamicTableField
                                {
                                    Name = sField.Name,
                                    Type = info.Type,
                                    MaxLength = info.MaxLength,
                                    SchemaType = sField.TypeNode!
                                });
                            }
                        }
                    }
                    else
                    {
                        fields.Add(new DynamicTableField
                        {
                            Name = DYNAMIC_TABLE_VALUE_FIELD,
                            Type = DynamicTableFieldType.Json,
                            SchemaType = node
                        });
                    }
                    break;
                }
            default:
                throw new ArgumentOutOfRangeException();
        }
        return new DynamicTableSchema
        {
            Name = DynamicTableName,
            DataType = Type,
            SchemaType = node,
            Single = single,
            Fields = fields,
            Indexes = indexes,
            IncrUpdate = IncrUpdate ?? false,
        };
    }

    // Get scalar type mapping info
    static DataTypeInfo GetDataTypeInfo(AnySchemeType node, StructFieldConfig? field = null)
    {
        if (node is ScalarType scalar)
        {
            // Get base scalar data type info
            DataTypeInfo info = JsonDataType;
            if (scalar.IsNumber)
            {
                decimal? upLimit = !string.IsNullOrEmpty(field?.UpLimit) && decimal.TryParse(field.UpLimit, out decimal u) ? u : scalar.UpLimit;
                decimal? lowLimit = !string.IsNullOrEmpty(field?.LowLimit) && decimal.TryParse(field.LowLimit, out decimal l) ? l : scalar.LowLimit;

                // Check Number value
                if (scalar.IsInt)
                {
                    // No Limit
                    if (!upLimit.HasValue || !lowLimit.HasValue)
                    {
                        info = new DataTypeInfo
                        {
                            Type = DynamicTableFieldType.BigInt
                        };
                    }
                    // Check Range
                    else if (lowLimit >= 0)
                    {
                        // Unsigned
                        decimal maxVal = upLimit.Value;
                        info = new DataTypeInfo
                        {
                            Type = maxVal switch
                            {
                                <= 0xffff => DynamicTableFieldType.USmallint,
                                <= 0xffffff => DynamicTableFieldType.UMediumint,
                                <= 0xffffffff => DynamicTableFieldType.UInt,
                                _ => DynamicTableFieldType.UBigInt
                            }
                        };
                    }
                    else
                    {
                        // Signed
                        decimal maxVal = Math.Max(Math.Abs(lowLimit.Value), Math.Abs(upLimit.Value));
                        info = new DataTypeInfo
                        {
                            Type = maxVal switch
                            {
                                <= 0x7fff => DynamicTableFieldType.Smallint,
                                <= 0x7fffff => DynamicTableFieldType.Mediumint,
                                <= 0x7fffffff => DynamicTableFieldType.Int,
                                _ => DynamicTableFieldType.BigInt
                            }
                        };
                    }
                }
                else if (scalar.IsSingle)
                {
                    info = new DataTypeInfo
                    {
                        Type = DynamicTableFieldType.Float
                    };
                }
                else
                {
                    info = new DataTypeInfo
                    {
                        Type = DynamicTableFieldType.Double
                    };
                }
            }
            else if (scalar.IsBool)
            {
                info = new DataTypeInfo
                {
                    Type = DynamicTableFieldType.Bool
                };
            }
            else if (scalar.IsString)
            {
                decimal? upLimit = !string.IsNullOrEmpty(field?.UpLimit) && decimal.TryParse(field.UpLimit, out decimal u) ? u : scalar.UpLimit;
                info = new DataTypeInfo
                {
                    Type = upLimit.HasValue ? upLimit.Value switch
                    {
                        <= 0xffff => DynamicTableFieldType.VarChar,
                        <= 0xffffff => DynamicTableFieldType.MediumText,
                        _ => DynamicTableFieldType.LongText
                    } : DynamicTableFieldType.LongText,
                    MaxLength = upLimit < 0xffff ? (int)upLimit.Value : null
                };
            }
            else if (scalar.IsDate)
            {
                info = new DataTypeInfo
                {
                    Type = DynamicTableFieldType.DateTime
                };
            }
            return info;
        }
        else if (node is EnumType @enum)
        {
            return new DataTypeInfo
            {
                Type = @enum.ValueType switch
                {
                    EnumValueType.String => DynamicTableFieldType.VarChar,
                    EnumValueType.Int => DynamicTableFieldType.BigInt,
                    EnumValueType.Flags => DynamicTableFieldType.UInt,
                    _ => throw new ArgumentOutOfRangeException()
                },
                MaxLength = @enum.ValueType == EnumValueType.String ? ENTITY_PRIMARY_KEY_MAX_LEN : null
            };
        }

        // Use Json for all others
        return JsonDataType;
    }

    static readonly DataTypeInfo JsonDataType = new()
    {
        Type = DynamicTableFieldType.Json
    };

    /// <summary>
    /// Validate field by DynamicTableSchema and return nullable
    /// </summary>
    public async Task<(bool isEmpty, AnySchemaNode? result, JsonNode? error)> ValidateDataAsync(SchemaContext context, JsonNode? token)
    {
        if (token.IsEmpty()) return (true, null, null);
        
        (AnySchemaNode? value, JsonNode? error) = await SchemaType!.ValidateValueAsync(context, token!);
        return (false, value, error);
    }

    #endregion
}

#region Schema Types

/// <summary>
/// The node func arguments
/// </summary>
public class AppFieldNodeArgument
{
    /// <summary>
    /// The application field
    /// </summary>
    public required AppFieldType AppField { get; init; }

    /// <summary>
    /// The data field
    /// </summary>
    public string? DataField { get; init; }
}


/// <summary>
/// The app relation schema
/// </summary>
public class AppRelationSchema
{
    /// <summary>
    /// The application field
    /// </summary>
    public required string AppField { get; init; }

    /// <summary>
    /// The data field
    /// </summary>
    public string DataField { get; init; } = string.Empty;

    /// <summary>
    ///  The relation type
    /// </summary>
    public RelationType Type { get; init; } = RelationType.Default;

    /// <summary>
    /// The function name
    /// </summary>
    public required string Func { get; init; }

    /// <summary>
    /// The function arguments
    /// </summary>
    public AppArgSchema[] Args { get; init; } = [];

    /// <summary>
    /// The field node
    /// </summary>
    [JsonIgnore]
    public AppFieldType? FieldNode { get; set; }

    /// <summary>
    /// The function node
    /// </summary>
    [JsonIgnore]
    public FunctionType? FunctionNode { get; set; }

    /// <summary>
    /// The relation status
    /// </summary>
    public SchemaNodeStatus Status { get; set; } = SchemaNodeStatus.Ready;
}


/// <summary>
/// The app argument
/// </summary>
public class AppArgSchema
{
    /// <summary>
    /// The application field
    /// </summary>
    public string? AppField { get; init; }

    /// <summary>
    /// The data field
    /// </summary>
    public string? DataField { get; init; }

    /// <summary>
    /// The json value
    /// </summary>
    public JsonNode? Value { get; init; }
}

#endregion

#region Dynamic Data Helpers

/// <summary>
/// The dynamic table field type
/// </summary>
public enum DynamicTableFieldType
{
    /// <summary>
    /// The bool field type
    /// </summary>
    Bool,

    /// <summary>
    /// The small int field type, (-32768��32767)
    /// </summary>
    Smallint,

    /// <summary>
    /// The unsigned small int field type, (0��65535)
    /// </summary>
    USmallint,

    /// <summary>
    /// The medium int field type, 	(-8388608��8388607)
    /// </summary>
    Mediumint,

    /// <summary>
    /// The unsigned medium int field type, (0��16777215)
    /// </summary>
    UMediumint,

    /// <summary>
    /// The int field type, (-2147483648��2147483647)
    /// </summary>
    Int,

    /// <summary>
    /// The unsigned int field type, (0��4294967295)
    /// </summary>
    UInt,

    /// <summary>
    /// The big int field type, (-9,223,372,036,854,775,808��9223372036854775807)
    /// </summary>
    BigInt,

    /// <summary>
    /// The unsigned big int field type, (0��18446744073709551615)
    /// </summary>
    UBigInt,

    /// <summary>
    /// The float field type
    /// </summary>
    Float,

    /// <summary>
    /// The float field type
    /// </summary>
    Double,

    /// <summary>
    /// The date time type
    /// </summary>
    DateTime,

    /// <summary>
    /// The tiny binary string (0, 255)
    /// </summary>
    TinyBlob,

    /// <summary>
    /// The binary string (0, 65535)
    /// </summary>
    Blob,

    /// <summary>
    /// The medium blob string (0, 16777215)
    /// </summary>
    MediumBlob,

    /// <summary>
    /// The long blob string (0, 4294967295)
    /// </summary>
    LongBlob,

    /// <summary>
    /// The fix-length string (0, 255)
    /// </summary>
    Char,

    /// <summary>
    /// The variable-length string (0, 65535)
    /// </summary>
    VarChar,

    /// <summary>
    /// The short text (0, 255)
    /// </summary>
    TinyText,

    /// <summary>
    /// The text string (0, 65535)
    /// </summary>
    Text,

    /// <summary>
    /// The medium text (0, 16777215)
    /// </summary>
    MediumText,

    /// <summary>
    /// The long text (0, 4294967295)
    /// </summary>
    LongText,

    /// <summary>
    /// The json field type
    /// </summary>
    Json,
}

/// <summary>
/// The operation to modify the dynamic table values
/// </summary>
public enum TransactionChangeOperation
{
    Create,
    Modify,
    Delete,
    DropAll
}

/// <summary>
/// The dynamic table structure
/// </summary>
public class DynamicTableSchema
{
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
    public List<DynamicTableField> Fields { get; init; } = [];

    /// <summary>
    /// The dynamic table indexes
    /// </summary>
    public DataIndex[]? Indexes { get; init; } = [];

    /// <summary>
    /// The data type node
    /// </summary>
    public required AnySchemeType SchemaType { get; init; }

    /// <summary>
    /// Append the fields to the string builder
    /// </summary>
    public StringBuilder AppendFields(StringBuilder sb, string prefix="")
    {
        bool appendComma = false;
        foreach (DynamicTableField dyfld in Fields)
        {
            if (appendComma)
                sb.Append(", ");
            appendComma = true;
            sb.Append($"{prefix}`{dyfld.Name}`");
        }
        return sb;
    }

    /// <summary>
    /// Gets the field values by the fields
    /// </summary>
    public IEnumerable<(string field, string? value, bool isString, bool isList)> GetFieldValues(JsonObject pack, bool primaryOnly = false, bool noPrimary = false)
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
                    // In value list
                    if (field.Type != DynamicTableFieldType.Json && pack[field.Name] is JsonArray arr)
                    {
                        if (arr.Count > 1)
                        {
                            if (field.IsString)
                            {
                                yield return (field.Name, $"({string.Join(",", arr.Select(v => $"\"{field.ToString(v)}\""))})", true, true);
                            }
                            else
                            {
                                yield return (field.Name, $"({string.Join(",", arr.Select(v => field.ToString(v)))})", false, true);
                            }
                        }
                        else
                        {
                            yield return (field.Name, field.ToString(arr[0]), field.IsString, false);
                        }
                    }
                    // Single value
                    else
                    {
                        yield return (field.Name, field.ToString(pack[field.Name]), field.IsString, false);
                    }
                }
                else
                {
                    yield return (field.Name, null, field.IsString, false);
                }
            }
            else if (pack.ContainsKey(field.Complex.Main) && pack[field.Complex.Main] is JsonObject sPack && sPack.ContainsKey(field.Complex.Field) && !sPack[field.Complex.Field].IsEmpty())
            {
                // In value list
                if (field.Type != DynamicTableFieldType.Json && sPack[field.Complex.Field] is JsonArray arr)
                {
                    if (arr.Count > 1)
                    {
                        if (field.IsString)
                        {
                            yield return (field.Name, $"({string.Join(",", arr.Select(v => $"\"{field.ToString(v)}\""))})", field.IsString, true);
                        }
                        else
                        {
                            yield return (field.Name, $"({string.Join(",", arr.Select(v => field.ToString(v)))})", field.IsString, true);
                        }
                    }
                    else
                    {
                        yield return (field.Name, field.ToString(arr[0]), field.IsString, false);
                    }
                }
                // Single value
                {
                    yield return (field.Name, field.ToString(sPack[field.Complex.Field]), field.IsString, false);
                }
            }
            else
            {
                yield return (field.Name, null, field.IsString, false);
            }
        }
    }

    public string? GetPrimaryKey(StructTypeNode pack)
    {
        List<string> keys = [];
        foreach((string _, string? value, bool _, bool _) in GetFieldValues(pack, true))
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            keys.Add(value);
        }
        return string.Join(":", keys);
    }
    
    public string? GetPrimaryKey(JsonObject pack)
    {
        List<string> keys = [];
        foreach((string _, string? value, bool _, bool _) in GetFieldValues(pack, true))
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            keys.Add(value);
        }
        return string.Join(":", keys);
    }


    public IEnumerable<(string field, string? value, bool isString, bool isList)> GetFieldValues(StructTypeNode pack, bool primaryOnly = false, bool noPrimary = false)
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
                    // In value list
                    if (field.Type != DynamicTableFieldType.Json && fieldNode is ArrayTypeNode arr)
                    {
                        if (arr.Count > 1)
                        {
                            if (field.IsString)
                            {
                                yield return (field.Name, $"({string.Join(",", arr.Select(v => $"\"{field.ToString(v)}\""))})", true, true);
                            }
                            else
                            {
                                yield return (field.Name, $"({string.Join(",", arr.Select(v => field.ToString(v)))})", false, true);
                            }
                        }
                        else
                        {
                            yield return (field.Name, field.ToString(arr[0]), field.IsString, false);
                        }
                    }
                    // Single value
                    else
                    {
                        yield return (field.Name, field.ToString(fieldNode), field.IsString, false);
                    }
                }
                else
                {
                    yield return (field.Name, null, field.IsString, false);
                }
            }
            else
            {
                AnySchemaNode? complex = pack.GetField(field.Complex.Main);
                if (complex is StructTypeNode sPack && sPack.GetField(field.Complex.Field) is { IsEmpty: false } part)
                {
                    // In value list
                    if (field.Type != DynamicTableFieldType.Json && part is ArrayTypeNode arr)
                    {
                        if (arr.Count > 1)
                        {
                            if (field.IsString)
                            {
                                yield return (field.Name, $"({string.Join(",", arr.Select(v => $"\"{field.ToString(v)}\""))})", field.IsString, true);
                            }
                            else
                            {
                                yield return (field.Name, $"({string.Join(",", arr.Select(v => field.ToString(v)))})", field.IsString, true);
                            }
                        }
                        else
                        {
                            yield return (field.Name, field.ToString(arr[0]), field.IsString, false);
                        }
                    }
                    // Single value
                    {
                        yield return (field.Name, field.ToString(part), field.IsString, false);
                    }
                }
                else
                {
                    yield return (field.Name, null, field.IsString, false);
                }
            }
        }
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

/// <summary>
/// The dynamic table field info
/// </summary>
public class DynamicTableField
{
    /// <summary>
    /// The field name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The dynamic field type
    /// </summary>
    public DynamicTableFieldType Type { get; init; }

    /// <summary>
    /// The complex field info
    /// </summary>
    public DataFieldComplexInfo? Complex { get; init; }

    /// <summary>
    /// Whether the field is primary
    /// </summary>
    public bool Primary { get; init; }

    /// <summary>
    /// The max length of the string type
    /// </summary>
    public int? MaxLength { get; init; }

    /// <summary>
    /// The data dict type
    /// </summary>
    public required AnySchemeType SchemaType { get; init; }

    /// <summary>
    /// The struct field node of primary field
    /// </summary>
    public StructFieldConfig? StructFieldNode { get; init; }

    /// <summary>
    /// Whether the type is string data
    /// </summary>
    public bool IsString => Type switch
    {
        DynamicTableFieldType.Bool => false,
        DynamicTableFieldType.Smallint => false,
        DynamicTableFieldType.USmallint => false,
        DynamicTableFieldType.Mediumint => false,
        DynamicTableFieldType.UMediumint => false,
        DynamicTableFieldType.Int => false,
        DynamicTableFieldType.UInt => false,
        DynamicTableFieldType.BigInt => false,
        DynamicTableFieldType.UBigInt => false,
        DynamicTableFieldType.Float => false,
        DynamicTableFieldType.Double => false,
        _ => true
    };

    /// <summary>
    /// Get JToken from reader
    /// </summary>
    public AnySchemaNode? FromReader(DbDataReader reader, int col = 0)
    {
        if (reader.IsDBNull(col)) return null;
        object? data;
        if (Type == DynamicTableFieldType.Json)
        {
            data = JsonNode.Parse(reader.GetString(col));
        }
        else
        {
            data = (Type switch
            {
                DynamicTableFieldType.Bool => reader.GetByte(col) == 1,
                DynamicTableFieldType.Smallint => reader.GetInt16(col),
                DynamicTableFieldType.USmallint => reader.GetInt32(col),
                DynamicTableFieldType.Mediumint => reader.GetInt32(col),
                DynamicTableFieldType.UMediumint => reader.GetInt32(col),
                DynamicTableFieldType.Int => reader.GetInt32(col),
                DynamicTableFieldType.UInt => reader.GetInt64(col),
                DynamicTableFieldType.BigInt => reader.GetInt64(col),
                DynamicTableFieldType.UBigInt => reader.GetInt64(col),
                DynamicTableFieldType.Float => reader.GetFloat(col),
                DynamicTableFieldType.Double => reader.GetDouble(col),
                DynamicTableFieldType.DateTime => reader.GetDateTime(col),
                _ => reader.GetString(col)
            });
        }

        return SchemaType.CreateNode(data);
    }

    /// <summary>
    /// Gets the string of the JToken value
    /// </summary>
    public string? ToString(AnySchemaNode? value)
    {
        if (value == null || value.IsEmpty) return null;

        return Type switch
        {
            DynamicTableFieldType.Bool => value.ToValue<bool>() ? "1" : "0",
            DynamicTableFieldType.DateTime => value.ToValue<DateTime>().ToString("yyyy-MM-dd HH:mm:ss"),
            _ => value.ToString()
        };
    }

    /// <summary>
    /// Gets the string of the JToken value
    /// </summary>
    public string? ToString(object? value)
    {
        if (value == null) return null;

        return Type switch
        {
            DynamicTableFieldType.Bool => Convert.ToBoolean(value) ? "1" : "0",
            DynamicTableFieldType.DateTime => Convert.ToDateTime(value).ToString("yyyy-MM-dd HH:mm:ss"),
            _ => value.ToString()
        };
    }

    /// <summary>
    /// Gets the string of the JToken value
    /// </summary>
    public string? ToString(JsonNode? value)
    {
        if (value == null && value.IsEmpty()) return null;
        JsonNode v = value!;
        return Type switch
        {
            DynamicTableFieldType.Bool => v.GetValue<bool>() ? "1" : "0",
            DynamicTableFieldType.DateTime => v.GetValue<DateTime>().ToString("yyyy-MM-dd HH:mm:ss"),
            DynamicTableFieldType.Json => v.ToJsonString(),
            _ => v.ToValue<string>()
        };
    }
}

/// <summary>
/// The complex field info
/// </summary>
public class DataFieldComplexInfo
{
    /// <summary>
    /// The complex main name
    /// </summary>
    public required string Main { get; init; }

    /// <summary>
    /// The complex field name
    /// </summary>
    public required string Field { get; init; }
}

/// <summary>
/// The data type map info
/// </summary>
public class DataTypeInfo
{
    /// <summary>
    /// The dynamic field type
    /// </summary>
    public DynamicTableFieldType Type { get; init; }

    /// <summary>
    /// The max length of the string type
    /// </summary>
    public int? MaxLength { get; init; }
}

/// <summary>
/// The field data change info
/// </summary>
internal record FieldDataChangeData(TransactionChangeOperation Operation, AnySchemaNode? Value, AnySchemaNode? Origin);

// The transaction change data
internal class TransactionChangeData
{
    /// <summary>
    /// The change operations
    /// </summary>
    public Dictionary<AppFieldType, List<FieldDataChangeData>> Changes { get; } = new();
}

/// <summary>
/// The push levels
/// </summary>
internal class FieldDataPushLevel
{
    /// <summary>
    /// The fields to be updated
    /// </summary>
    public List<AppFieldType> Fields { get; } = new();

    /// <summary>
    /// The next level to be updated
    /// </summary>
    public FieldDataPushLevel? Next { get; set; }
}

/// <summary>
/// The push argument
/// </summary>
internal struct FieldDataPushArg
{
    /// <summary>
    /// The value
    /// </summary>
    public AnySchemaNode? Value { get; set; }

    /// <summary>
    /// The origin value
    /// </summary>
    public AnySchemaNode? Origin { get; set; }

    /// <summary>
    /// Whether is array data
    /// </summary>
    public bool IsArray => Type is ArrayType;

    /// <summary>
    /// The value type
    /// </summary>
    public AnySchemeType Type { get; set; }

    /// <summary>
    /// Whether is full data
    /// </summary>
    public bool IsFull { get; set; }

    /// <summary>
    /// Whether the data is changed
    /// </summary>
    public bool Changed { get; set; }
}

#endregion