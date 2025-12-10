using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SchemaNode.Components;
using SchemaNode.Node;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;
using Microsoft.AspNetCore.Identity.UI.Services;
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable MemberCanBePrivate.Global

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
    public int Seqno { get; private init; }

    /// <summary>
    /// The field name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The field type.
    /// </summary>
    public string Type { get; init; } = null!;

    /// <summary>
    /// The field chinese name.
    /// </summary>
    public LocaleString? Display { get; init; }

    /// <summary>
    /// The description of the field.
    /// </summary>
    public LocaleString? Desc { get; private init; }

    /// <summary>
    /// The source application
    /// </summary>
    public string? SourceApp { get; private init; }

    /// <summary>
    /// The source field
    /// </summary>
    public string? SourceField { get; private init; }

    /// <summary>
    /// Track the push data to the source field, so toggle the source target, will also re-push the data
    /// </summary>
    public bool? TrackPush { get; private init; }
    
    /// <summary>
    /// The calculate function
    /// </summary>
    public string? Func { get; init; }

    /// <summary>
    /// The input fields
    /// </summary>
    public string[]? Args { get; init; }

    /// <summary>
    /// The authentication policy, normally row policy
    /// </summary>
    public PolicyItem[]? Auths { get; private init; }

    /// <summary>
    /// Row filter policy
    /// </summary>
    public RowPolicyItem[]? RowAuths { get; private init; }

    /// <summary>
    /// The column access policy
    /// </summary>
    public ColPolicyItem[]? ColAuths { get; private init; }

    /// <summary>
    /// The field is using increase update, no full data push allowed
    /// </summary>
    public bool? IncrUpdate { get; private init; }

    /// <summary>
    /// The field is front-end only, no data storage
    /// </summary>
    public bool? Frontend { get; private init; }

    /// <summary>
    /// The field is disabled
    /// </summary>
    public bool? Disable { get; private init; }
    
    /// <summary>
    /// The field is readonly
    /// </summary>
    public bool? Readonly { get; private init; }

    /// <summary>
    /// The combine rule for scalar/enum type
    /// </summary>
    public DataCombineType? Combine { get; private init; }

    /// <summary>
    /// The combine rule for struct or struct-array type
    /// </summary>
    public DataCombine[]? Combines { get; private init; }

    /// <summary>
    /// The additional data
    /// </summary>
    public Dictionary<string, JsonElement>? Additional { get; private init; }

    #endregion

    #region States

    /// <summary>
    /// The application field node status
    /// </summary>
    public SchemaNodeStatus? Status { get; internal set; }

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
    public AppType Application { get; internal set; } = null!;
    
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
    
    /// <summary>
    /// The field is single value
    /// </summary>
    public bool Single => SchemaType is not ArrayType arr || arr.Primary == null || arr.Primary.Length == 0;

    #endregion
    
    #region Method
    
    /// <summary>
    /// Add observer
    /// </summary>
    public void AddObserver(AppFieldType observer)
    {
        _observers ??= [];
        if (!_observers.Contains(observer))
            _observers.Add(observer);
    }
    
    /// <summary>
    /// Gets the authentication policies with the scope
    /// </summary>
    public IEnumerable<PolicyItem> GetAuthPolicies(PolicyScope scope)
    {
        // Application policy first
        foreach (var i in Application.GetAuthPolicies(scope)) yield return i;

        // self policies
        var item = Auths?.FirstOrDefault(i => i.Scope == scope);
        if (item != null) yield return item;
    }

    /// <summary>
    /// Gets the field authentication policies with the scope
    /// </summary>
    public IEnumerable<string> GetColPolicies(string fieldName)
    {
        ColPolicyItem? item = ColAuths?.FirstOrDefault(i => i.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
        if (item == null || item.Evaluators == null || item.Evaluators.Length == 0) yield break;
        foreach (var evaluator in item.Evaluators)
            yield return evaluator;
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
            Auths = entity.Auths,
            RowAuths = entity.RowAuths,
            ColAuths = entity.ColAuths,
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
            Status = entity.Status,
            SourceApp = entity.SourceApp,
            SourceField = entity.SourceField,
            TrackPush = entity.TrackPush,
            Func = entity.Func,
            Args = entity.Args,
            Auths = entity.Auths,
            RowAuths = entity.RowAuths,
            ColAuths = entity.ColAuths,
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
        List<DynamicTableField> fields = [];
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

#region Dynamic Data Helpers

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

/// <summary>
/// The data type map info
/// </summary>
class DataTypeInfo
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
#endregion