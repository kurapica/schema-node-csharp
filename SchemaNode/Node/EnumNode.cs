using System.Reflection;
using System.Text.Json.Nodes;
using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Node;

/// <summary>
/// The in-memory enum schema representation
/// </summary>
public class EnumNode: NamespaceNode
{
    // ReSharper disable once InconsistentNaming
    private const int MAX_SUBLIST_LEVEL = 3;
    
    #region Data
    
    /// <summary>
    /// The enum value type
    /// </summary>
    public EnumValueType ValueType { get; set; } = EnumValueType.String;

    /// <summary>
    /// The cascade list
    /// </summary>
    public string[]? Cascade { get; set; }
    
    /// <summary>
    /// The root for all enum values
    /// </summary>
    public EnumValueInfo Root { get; set; } = new ();

    #endregion
    
    #region Status

    /// <inheritdoc />
    public override SchemaType Type => SchemaType.Enum;
    
    /// <summary>
    /// The max flags value
    /// </summary>
    public long MaxFlags { get; set; }

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
            lock (this)
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
        lock (this)
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
    /// <returns></returns>
    public async Task<EnumValueAccess[]> LoadEnumAccessListAsync(SchemaContext context, string value, bool? noSubList)
    {
        EnumValueInfo[] accesses = await LoadEnumValueAccessAsync(context, value);
        EnumValueAccess[] result = new EnumValueAccess[accesses.Length - 1];
        for (int i = 0; i < accesses.Length - 1; i++)
        {
            result[i] = new EnumValueAccess
            {
                Value = accesses[i + 1].Value,
                Name = Cascade?[i],
                SubList = (noSubList ?? false) ? null : accesses[i].SubList?.Select(a => a.Clone()).ToArray()
            };
        }
        
        return result;
    }

    /// <inheritdoc />
    public override async Task<(JsonNode? value, JsonNode? error)> ValidateValueAsync(SchemaContext context, JsonNode value)
    {
        if (value is not JsonValue val || val.IsEmpty())
            return (value, TYPE_VALUE_NOT_VALID);

        // Combine value
        if (ValueType == EnumValueType.Flags)
        {
            try
            {
                long total = val.GetValue<long>();
                if (total < 0) return (value, TYPE_VALUE_NOT_VALID);

                if (!Root.IsFullyLoaded)
                {
                    EnumValueInfo[] infos = await context.LoadEnumSubListAsync(this, null);
                    lock (this)
                    {
                        Root.SubList = infos;
                        Root.CheckFullyLoadedStatus();
                        UpdateMaxFlags();
                    }
                }    
                return MaxFlags > total ? (total, null) : (value, TYPE_VALUE_NOT_VALID);
            }
            catch
            {
                return (value, TYPE_VALUE_NOT_VALID);
            }
        }
        
        EnumValueInfo[] access = await LoadEnumValueAccessAsync(context, value.ToString());
        if (access.Length > 0)
        {
            return (ValueType switch
            {
                EnumValueType.String => val.GetValue<string>(),
                EnumValueType.Int => val.GetValue<int>(),
                EnumValueType.Float => val.GetValue<float>(),
                EnumValueType.Double => val.GetValue<double>(),
                EnumValueType.Flags => val.GetValue<int>(),
                _ => throw new ArgumentOutOfRangeException()
            }, null);
        }
        else
        {
            return (value, TYPE_VALUE_NOT_VALID);
        }
    }

    /// <inheritdoc />
    public override bool CanBeUseAs(NamespaceNode other) => 
        base.CanBeUseAs(other) ||
        other switch
        {
            ScalarNode scalar => ValueType switch
            {
                EnumValueType.String => scalar.IsString,
                EnumValueType.Int => scalar.IsInt,
                EnumValueType.Float => scalar.IsNumber,
                EnumValueType.Double => scalar.IsNumber,
                EnumValueType.Flags => scalar.IsInt,
                _ => false
            },
            _ => false
        };

    /// <inheritdoc />
    public override bool IsIndexable => ValueType is EnumValueType.String or EnumValueType.Int or EnumValueType.Float;

    #endregion

    #region Static Feature

    /// <summary>
    /// Generate system enum
    /// </summary>
    public static NodeSchema[] GenerateSystemEnum(Type type, string? ns = null)
    {
        SchemaEnumAttribute? attr = type.GetCustomAttribute<SchemaEnumAttribute>();
        if (!type.IsEnum) return [];

        EnumValueType valueType = attr?.ValueType ?? EnumValueType.Int;
        NodeSchema enumSchema = new NodeSchema
        {
            Name = $"{(string.IsNullOrWhiteSpace(ns) ? "" : $"{ns}.")}{(attr?.Type ?? type.Name).ToLowerInvariant()}",
            Type = SchemaType.Enum,
            Display = attr?.Display ?? type.Name,
            Enum = new EnumSchema
            {
                Type = valueType,
                Values = System.Enum.GetValues(type).Cast<object>().Select(t =>
                {
                    string name = System.Enum.GetName(type, t)!;
                    return new EnumValueInfo
                    {
                        Name = name,
                        Value = valueType switch
                        {
                            EnumValueType.String => name,
                            _ => $"{t}"
                        },
                        HasSubList = false,
                    };
                }).ToArray(),
            }
        };
        
        return (attr?.Array ?? false) ? [ enumSchema, new NodeSchema
        {
            Name = $"{enumSchema.Name}s",
            Type = SchemaType.Array,
            Display = $"[Array]{enumSchema.Display.Key}",
            Array = new ArraySchema
            {
                Element = enumSchema.Name
            }
        } ] : [ enumSchema ];
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
    
    #endregion
}