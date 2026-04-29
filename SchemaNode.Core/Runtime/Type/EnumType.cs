using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json.Nodes;
using SchemaNode.Service;
using SchemaNode.Struct;
using JsonNode = System.Text.Json.Nodes.JsonNode;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory enum schema representation
/// </summary>
public sealed class EnumType: ValueType
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
    
    #endregion
    
    #region Status

    /// <summary>
    /// The max flags value
    /// </summary>
    long MaxFlags { get; set; }

    /// <summary>
    /// The root for all enum values
    /// </summary>
    EnumValueInfo Root { get; set; } = new();

    /// <summary>
    /// The enum value cache
    /// </summary>
    ConcurrentDictionary<string, EnumValueInfo> valueMaps = new(StringComparer.OrdinalIgnoreCase);

    #endregion
    
    #region Method

    /// <inheritdoc />
    public override Task LoadAsync(SchemaContext context, NodeSchema schema, bool preload = false)
    {
        EnumSchema? @enum = schema.Enum;

        // Data
        valueMaps.Clear();
        ValueType = @enum?.Type ?? EnumValueType.String;
        Cascade = @enum?.Cascade;
        Root = new EnumValueInfo
        {
            SubList = @enum?.Values
        };
        UpdateLoadState(Root, reset: true);
        UpdateMaxFlags();
        
        // Status
        if (@enum == null) ErrorCode = SchemaNodeStatus.NoDefinition;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Ges the enu value info
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public async Task<EnumValueInfo?> LoadEnumValueInfo(SchemaContext context, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (valueMaps.TryGetValue(value, out var node)) return node;

        EnumValueInfo[] accesses = await LoadEnumValueAccessAsync(context, value);
        return accesses.Length > 0 ? accesses.Last() : null;
    }

    /// <summary>
    /// Load the enum value access path
    /// </summary>
    public EnumValueInfo[] LoadCachedEnumValueAccessAsync(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];

        if (valueMaps.TryGetValue(value, out var node))
        {
            EnumValueInfo[] temp = new EnumValueInfo[node.Level + 1];
            temp[node.Level] = node;
            for (int i = node.Level - 1; i >= 0; i--)
            {
                if (node.Parent == null) return [];
                node = node.Parent;
                temp[i] = node;
            }
            return temp;
        }
        return [];
    }

    /// <summary>
    /// Load the enum value access path
    /// </summary>
    public async Task<EnumValueInfo[]> LoadEnumValueAccessAsync(SchemaContext context, string? value)
    {
        EnumValueInfo[]? accesses = [];
        if (string.IsNullOrWhiteSpace(value)) return [];

        // Try to get from cache
        if (getAccess(value, out accesses))
            return accesses ?? [];

        // Load from the provider
        EnumValueAccess[] accessList = await context.LoadEnumAccessListAsync(this, value!, false, true);
        if (accessList.Length == 0) return []; // not exist

        // combine the access list
        lock (_lock)
        {
            Root.CombineAccessList(accessList);
            UpdateLoadState(Root);
        }

        // Ignore the value not exist after loading
        return getAccess(value, out accesses) ? accesses ?? [] : [];

        bool getAccess(string value, out EnumValueInfo[]? accesses)
        {
            accesses = null;
            if (valueMaps.TryGetValue(value, out var node))
            {
                var temp = new EnumValueInfo[node.Level + 1];
                temp[node.Level] = node;
                for (int i = node.Level - 1; i >= 0; i--)
                {
                    if (node.Parent == null) return false;
                    node = node.Parent;
                    temp[i] = node;
                }
                accesses = temp;
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Load the enum value sub list
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="value">The root enum value, optional</param>
    /// <param name="fullList">Whether try to load the full list</param>
    /// <returns></returns>
    public async Task<EnumValueInfo[]> LoadEnumSubListAsync(SchemaContext context, string? value, bool fullList = false)
    {
        if (string.IsNullOrWhiteSpace(value)) return Root.SubList ?? [];

        EnumValueInfo[] accesses = await LoadEnumValueAccessAsync(context, value);
        if (accesses.Length == 0) return [];
        EnumValueInfo access = accesses.Last();
        if (!(access.HasSubList ?? false)) return [];
         
        // load sub list
        int chkLvl = 1;
        if (fullList)
            chkLvl = Math.Min((Cascade?.Length ?? 1) - accesses.Length + 1, MAX_SUBLIST_LEVEL);
            
        // full-filled
        if (UpdateLoadState(access, chkLvl))
            return access.Clone(chkLvl).SubList ?? [];
            
        // load sub list
        EnumValueInfo[] subList = await context.LoadEnumSubListAsync(this, value!, true);
        lock (_lock)
        {
            access.SubList = subList;
            UpdateLoadState(access);
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

    /// <inheritdoc />
    public override async Task<(Node.DataNode? value, JsonNode? error)> ValidateValueAsync(SchemaContext context, JsonNode value, IReadOnlyList<IConstraintProperty>? constraints = null)
    {
        if (value is not JsonValue val || val.IsEmpty())
            return (null, TYPE_VALUE_NOT_VALID);

        Node.DataNode result = new EnumNode(this);

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
                        UpdateLoadState(Root);
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
        if (access.Length == 0)
            return (null, TYPE_VALUE_NOT_VALID);

        result.Value = ValueType switch
        {
            EnumValueType.String => val.ToString(),
            _ => val.ToValue<long>()
        };

        // Constraint validation
        if (Constraints is { Length: > 0 })
        {
            foreach (IConstraintProperty constraint in Constraints)
            {
                if (constraints != null && constraints.FirstOrDefault(c => c.GetType() == constraint.GetType()) is IConstraintProperty cst && cst.HasValue)
                {
                    if (await cst.ValidateEnumAsync(context, (EnumNode)result) == false)
                        return (null, TYPE_VALUE_NOT_VALID);
                }
                else if (await constraint.ValidateEnumAsync(context, (EnumNode)result) == false)
                    return (null, TYPE_VALUE_NOT_VALID);
            }
        }

        return (result, null);
    }

    /// <inheritdoc />
    public override bool CanBeUseAs(NodeType other, bool exactly = false) => 
        base.CanBeUseAs(other, exactly) 
        || other switch
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
            Type = NodeType.Enum,
            Display = typeAttr?.Display ?? type.GetSummaryFromXmlDoc() ?? typeName,
            Enum = new EnumSchema
            {
                Type = valueType,
                Values = type.GetFields(BindingFlags.Public | BindingFlags.Static).Select(f =>
                {
                    return new EnumValueInfo
                    {
                        Name = type.GetSummaryFromXmlDoc(f) ?? $"{typeName}.{f.Name.ToLower()}",
                        Value = valueType switch
                        {
                            EnumValueType.String => (f.GetCustomAttribute<EnumMemberAttribute>()?.Value ?? f.Name).ToCamelCase(),
                            _ => $"{f.GetValue(null)}"
                        },
                        HasSubList = false,
                    };
                }).ToArray(),
            }
        };

        if (Utility.SystemLocale.HasLocales)
        {
            Utility.SystemLocale.Translate(enumSchema.Display, enumSchema.Name);
            foreach (EnumValueInfo value in enumSchema.Enum!.Values)
                Utility.SystemLocale.Translate(value.Name);
        }

        return [ enumSchema ];
    }

    #endregion

    #region Utility

    /// <summary>
    /// Refresh status
    /// </summary>
    bool UpdateLoadState(EnumValueInfo node, int level = 999, EnumValueInfo? parent = null, bool reset = false)
    {
        if (node.IsFullyLoaded && !reset || level == 0) return true;
        node.IsFullyLoaded = false;
        valueMaps[node.Value] = node;

        // update ref
        if (parent != null)
        {
            node.Parent = parent;
            node.Level = parent.Level + 1;
        }

        // If loaded from static resources
        if (node.SubList is not null && node.SubList.Length > 0) node.HasSubList = true;

        if (node.HasSubList ?? false)
        {
            if (node.SubList is not null && node.SubList.Length > 0)
            {
                foreach (var item in node.SubList)
                    UpdateLoadState(item, level - 1, node, reset);
                node.IsFullyLoaded = node.SubList.All(x => x.IsFullyLoaded);
                return true;
            }
        }
        else
        {
            node.IsFullyLoaded = true;
        }

        return node.IsFullyLoaded;
    }

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

public static class EnumTypeExtensions
{
    
    /// <summary>
    /// Load the enum value sub list
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="node">The enum schema node</param>
    /// <param name="value">The root enum value, optional</param>
    /// <param name="fullList">Whether load the full list</param>
    /// <returns></returns>
    public static async Task<EnumValueSchema[]> LoadEnumSubListAsync(this SchemaContext context, EnumType node, string? value, bool? fullList = null)
    {
        if (node.SchemaProvider != null)
        {
            return await ((INodeSchemaProvider)context.GetRequiredService(node.SchemaProvider)).LoadEnumSubListAsync(node.Name, value, fullList);
        }
        foreach (INodeSchemaProvider provider in context.GetServices<INodeSchemaProvider>())
        {
            try
            {
                EnumValueSchema[] result = await provider.LoadEnumSubListAsync(node.Name, value, fullList);
                node.SchemaProvider = provider.GetType();
                return result;
            }
            catch
            {
                //pass
            }
        }
        return [];
    }

    /// <summary>
    /// Load the enum value access list from the server
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="node">The enum schema node</param>
    /// <param name="value">The enum value for access</param>
    /// <param name="noSubList">no sub list should be loaded</param>
    /// <param name="withSubList">with the value's sub list if existed</param>
    /// <returns></returns>
    public static async Task<EnumValueAccess[]> LoadEnumAccessListAsync(this SchemaContext context, EnumType node, string value, bool? noSubList = null, bool? withSubList = null)
    {
        if (node.SchemaProvider != null)
        {
            return await ((INodeSchemaProvider)context.GetRequiredService(node.SchemaProvider)).LoadEnumAccessListAsync(node.Name, value, noSubList, withSubList);
        }
        foreach (INodeSchemaProvider provider in context.GetServices<INodeSchemaProvider>())
        {
            try
            {
                EnumValueAccess[] result = await provider.LoadEnumAccessListAsync(node.Name, value, noSubList, withSubList);
                node.SchemaProvider = provider.GetType();
                return result;
            }
            catch
            {
                // pass
            }
        }
        return [];
    }

}