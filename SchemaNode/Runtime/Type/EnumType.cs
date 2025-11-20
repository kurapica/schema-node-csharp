using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Nodes;
using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory enum schema representation
/// </summary>
public class EnumType: AnySchemeType
{
    // ReSharper disable once InconsistentNaming
    private const int MAX_SUBLIST_LEVEL = 3;
    
    #region Data
    
    /// <summary>
    /// The enum value type
    /// </summary>
    public EnumValueType ValueType { get; internal set; } = EnumValueType.String;

    /// <summary>
    /// The cascade list
    /// </summary>
    public LocaleString[]? Cascade { get; internal set; }
    
    /// <summary>
    /// The root for all enum values
    /// </summary>
    public EnumValueInfo Root { get; private set; } = new ();
    
    /// <summary>
    /// The additional data
    /// </summary>
    public Dictionary<string, JsonElement>? Additional { get; internal set; }

    #endregion
    
    #region Status

    /// <inheritdoc />
    public override SchemaType Type => SchemaType.Enum;
    
    /// <summary>
    /// The max flags value
    /// </summary>
    public long MaxFlags { get; internal set; }

    #endregion
    
    #region Method

    /// <inheritdoc />
    public override Task LoadAsync(SchemaContext context, NodeSchema schema, bool preload = false)
    {
        EnumSchema? @enum = schema.Enum;
        
        // Data
        ValueType = @enum?.Type ?? EnumValueType.String;
        Cascade = @enum?.Cascade;
        Root = new EnumValueInfo
        {
            SubList = @enum?.Values
        };
        Additional = @enum?.Additional;
        Root.CheckFullyLoadedStatus();
        UpdateMaxFlags();
        
        // Status
        if (@enum == null) Status = SchemaNodeStatus.NoDefinition;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Load the enum value access path
    /// </summary>
    public async Task<EnumValueInfo[]> LoadEnumValueAccessAsync(SchemaContext context, string? value)
    {
        // check existed
        EnumValueInfo[]? accesses = Root.GetEnumAccesses(value);
        if (accesses == null)
        {
            EnumValueAccess[] accessList = await context.LoadEnumAccessListAsync(this, value!, false, true);
            if (accessList.Length == 0) return []; // not exist

            // combine the access list
            lock (_lock)
            {
                Root.CombineAccessList(accessList);
                Root.CheckFullyLoadedStatus();
            }
            
            // re check
            accesses = Root.GetEnumAccesses(value);
        }
        return accesses ?? [];
    }

    /// <summary>
    /// Load the enum value sub list
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="value">The root enum value, optional</param>
    /// <param name="fullList">Whether try to load the full list</param>
    /// <returns></returns>
    public async Task<EnumValueInfo[]> LoadEnumSubListAsync(SchemaContext context, string? value, bool? fullList)
    {
        EnumValueInfo[] accesses = await LoadEnumValueAccessAsync(context, value);
        EnumValueInfo access = accesses.Last();
        if (!(access.HasSubList ?? false)) return [];
         
        // load sub list
        int chkLvl = 1;
        if (fullList ?? false)
        {
            chkLvl = Math.Min((Cascade?.Length ?? 1) - accesses.Length + 1, MAX_SUBLIST_LEVEL);
        }
            
        // full-filled
        if (access.CheckFullyLoadedStatus(chkLvl))
            return access.Clone(chkLvl).SubList ?? [];
            
        // load sub list
        EnumValueInfo[] subList = await context.LoadEnumSubListAsync(this, value!, true);
        lock (_lock)
        {
            access.SubList = subList;
            access.CheckFullyLoadedStatus();
        }
        return access.Clone(chkLvl).SubList ?? [];

    }

    /// <summary>
    /// Load the enum value access list from the server
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="value">The enum value for access</param>
    /// <param name="noSubList">no sub list should be loaded</param>
    /// <param name="withSubList">with the value's sub list</param>
    /// <returns></returns>
    public async Task<EnumValueAccess[]> LoadEnumAccessListAsync(SchemaContext context, string value, bool? noSubList = false, bool? withSubList = false)
    {
        EnumValueInfo[] accesses = await LoadEnumValueAccessAsync(context, value);
        if (accesses.Length == 0) return [];
        
        withSubList = (withSubList ?? false) && accesses.Length < (Cascade?.Length ?? 1) && accesses.Last().SubList is { Length: > 0};
        EnumValueAccess[] result = new EnumValueAccess[withSubList.Value ? accesses.Length : (accesses.Length - 1)];
        for (int i = 0; i < accesses.Length - 1; i++)
        {
            result[i] = new EnumValueAccess
            {
                Value = accesses[i + 1].Value,
                Name = Cascade?[i],
                SubList = (noSubList ?? false) ? null : accesses[i].SubList?.Select(a => a.Clone()).ToArray()
            };
        }

        if (withSubList.Value)
        {
            result[accesses.Length - 1] = new EnumValueAccess
            {
                Value = "",
                Name = Cascade?[accesses.Length - 1],
                SubList = accesses.Last().SubList?.Select(a => a.Clone()).ToArray()
            };
        }
        
        return result;
    }

    /// <summary>
    /// Save enum value sub list
    /// </summary>
    internal void SaveEnumSubListAsync(string? value, EnumValueInfo[] values)
    {
        // check existed
        EnumValueInfo[]? accesses = Root.GetEnumAccesses(value);
        if (accesses is { Length: > 0 })
        {
            EnumValueInfo access = accesses.Last();
            if (values.Length == 0)
            {
                access.SubList = null;
                access.HasSubList = false;
            }
            else
            {
                if (access.SubList is not null)
                {
                    foreach (EnumValueInfo info in values)
                    {
                        EnumValueInfo? exist = access.SubList.FirstOrDefault(s => s.Value.Equals(info.Value, StringComparison.OrdinalIgnoreCase));
                        if (exist is null) continue;
                        info.HasSubList = exist.HasSubList;
                        info.SubList = exist.SubList;
                    }
                }
                access.HasSubList = true;
                access.SubList = values;
            }
        }
    }

    /// <inheritdoc />
    public override async Task<(AnySchemaNode? value, JsonNode? error)> ValidateValueAsync(SchemaContext context, JsonNode value)
    {
        if (value is not JsonValue val || val.IsEmpty())
            return (null, TYPE_VALUE_NOT_VALID);

        AnySchemaNode result = new EnumTypeNode(this);

        // Combine value
        if (ValueType == EnumValueType.Flags)
        {
            try
            {
                long total = val.GetValue<long>();
                if (total < 0) return (null, TYPE_VALUE_NOT_VALID);

                if (!Root.IsFullyLoaded)
                {
                    EnumValueInfo[] infos = await context.LoadEnumSubListAsync(this, null);
                    lock (_lock)
                    {
                        Root.SubList = infos;
                        Root.CheckFullyLoadedStatus();
                        UpdateMaxFlags();
                    }
                }
                if (MaxFlags > total)
                {
                    result.Value = total;
                    return (result, null);
                }
                else
                    return (null, TYPE_VALUE_NOT_VALID);
            }
            catch
            {
                return (null, TYPE_VALUE_NOT_VALID);
            }
        }
        
        EnumValueInfo[] access = await LoadEnumValueAccessAsync(context, value.ToString());
        if (access.Length > 0)
        {
            result.Value = ValueType switch
            {
                EnumValueType.String => val.ToString(),
                _ => val.ToValue<long>()
            };
            return (result, null);
        }
        else
        {
            return (null, TYPE_VALUE_NOT_VALID);
        }
    }

    /// <inheritdoc />
    public override bool CanBeUseAs(AnySchemeType other) => 
        base.CanBeUseAs(other) ||
        other switch
        {
            ScalarType scalar => ValueType switch
            {
                EnumValueType.String => scalar.IsString,
                EnumValueType.Int => scalar.IsInt,
                EnumValueType.Flags => scalar.IsInt,
                _ => false
            },
            _ => false
        };

    /// <inheritdoc />
    public override bool IsIndexable => ValueType is EnumValueType.String or EnumValueType.Int or EnumValueType.Flags;

    /// <summary>
    /// Convert to node schema
    /// </summary>
    public NodeSchema ToNodeSchema(int limitLevel = 0)
    {
        return ToSchema().With(new EnumSchema
        {
            Type = ValueType,
            Cascade = Cascade,
            Values = Root.SubList?.Select(a => a.Clone(limitLevel)).ToArray() ?? [],
            Additional = Additional,
        });
    }

    #endregion

    #region Static Feature

    /// <summary>
    /// Generate system enum
    /// </summary>
    public static NodeSchema[] GenerateSystemEnum(Type type, string? ns = null)
    {
        if (!type.IsEnum) return [];

        EnumValueType valueType = type.GetCustomAttribute<FlagsAttribute>() != null ? EnumValueType.Flags : EnumValueType.String;
        SchemaAttribute? typeAttr = type.GetCustomAttribute<SchemaAttribute>();
        string typeName = typeAttr?.Name ?? $"{(string.IsNullOrWhiteSpace(ns) ? "" : $"{ns}.")}{type.Name.ToLowerInvariant()}";
        NodeSchema enumSchema = new NodeSchema
        {
            Name = typeName,
            Type = SchemaType.Enum,
            Display = typeAttr?.Display ?? type.GetSummaryFromXmlDoc() ?? typeName,
            Enum = new EnumSchema
            {
                Type = valueType,
                Values = type.GetFields(BindingFlags.Public | BindingFlags.Static).Select(f =>
                {
                    string name = f.Name.ToLower();
                    return new EnumValueInfo
                    {
                        Name = $"{typeName}.{name}",
                        Value = valueType switch
                        {
                            EnumValueType.String => (f.GetCustomAttribute<EnumMemberAttribute>()?.Value ?? name).ToLower(),
                            _ => $"{f.GetValue(null)}"
                        },
                        HasSubList = false,
                    };
                }).ToArray(),
            }
        };
        
        return [ enumSchema ];
    }

    #endregion
    
    #region Utility

    void UpdateMaxFlags()
    {
        if (ValueType != EnumValueType.Flags || Root.SubList == null || Root.SubList.Length == 0) return;
        long max = 0;
        try
        {
            foreach (EnumValueInfo info in Root.SubList)
            {
                if (long.TryParse(info.Value, out long val))
                {
                    max = Math.Max(max, val);
                }
            }
        }
        catch
        {
            // pass
        }

        MaxFlags = max * 2;
    }

    private readonly Lock _lock = new();
    
    #endregion
    
    #region Conversion
    
    /// <summary>
    /// Convert the node to schema
    /// </summary>
    public static implicit operator NodeSchema?(EnumType? schema)
    {
        return schema?.ToNodeSchema();
    }
    
    #endregion
}