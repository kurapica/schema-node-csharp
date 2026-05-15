using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Collections.Concurrent;
using SchemaNode.Service;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory enum schema representation
/// </summary>
public sealed class EnumType: ValueType
{
    #region Fields
    
    // ReSharper disable once InconsistentNaming
    const int MAX_SUBLIST_LEVEL = 3;

    private readonly Lock _lock = new();

    // The enum schema
    private EnumSchema? _enumSchema;
    
    // The max flags value
    private long _maxFlags;

    // The root for all enum values
    private EnumValueSchema _root = new();

    // The enum value cache
    private readonly ConcurrentDictionary<string, EnumValueSchema> _valueMaps = new(StringComparer.OrdinalIgnoreCase);

    #endregion
    
    #region Method

    /// <inheritdoc />
    public override Task LoadAsync(SchemaContext context)
    {
        _enumSchema = GetPropertyValue<EnumSchema>();

        // Data
        _valueMaps.Clear();
        _root = new EnumValueSchema
        {
            SubList = _enumSchema?.Values
        };
        UpdateLoadState(_root, reset: true);
        UpdateMaxFlags();
        
        // Status
        if (_enumSchema == null) Error = ErrorCodes.NO_DEFINITION;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Load the enum value sub list
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="value">The root enum value, optional</param>
    /// <param name="fullList">Whether try to load the full list</param>
    /// <returns></returns>
    public async Task<EnumValueSchema[]> LoadEnumSubListAsync(SchemaContext context, string? value, bool fullList = false)
    {
        if (string.IsNullOrWhiteSpace(value)) 
            return _root.Clone(fullList ? (_enumSchema?.Cascade?.Length ?? 1) : 1).SubList ?? [];

        EnumValueSchema[] accesses = await LoadEnumValueAccessAsync(context, value);
        if (accesses.Length == 0) return [];
        EnumValueSchema access = accesses.Last();
        if (!(access.HasSubList ?? false)) return [];
         
        // load sub list
        int chkLvl = 1;
        if (fullList)
            chkLvl = Math.Min((_enumSchema?.Cascade?.Length ?? 1) - accesses.Length + 1, MAX_SUBLIST_LEVEL);
            
        // full-filled
        if (UpdateLoadState(access, chkLvl))
            return access.Clone(chkLvl).SubList ?? [];
        
        // load sub list
        if (Provider != null && context.GetRequiredService(Provider) is IEnumSchemaProvider provider)
        {
            EnumValueSchema[] subList = await provider.LoadEnumSubListAsync(Name, value, fullList);
            lock (_lock)
            {
                access.SubList = subList;
                UpdateLoadState(access);
            }
            return access.Clone(chkLvl).SubList ?? [];
        }
        return [];
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
        EnumValueSchema[] accesses = await LoadEnumValueAccessAsync(context, value);
        if (accesses.Length == 0) return [];
        
        withSubList = (withSubList ?? false) && accesses.Length < (_enumSchema?.Cascade?.Length ?? 1) && accesses.Last().SubList is { Length: > 0};
        EnumValueAccess[] result = new EnumValueAccess[withSubList.Value ? accesses.Length : (accesses.Length - 1)];
        for (int i = 0; i < accesses.Length - 1; i++)
        {
            result[i] = new EnumValueAccess
            {
                Value = accesses[i + 1].Value,
                Name = _enumSchema?.Cascade?[i],
                SubList = (noSubList ?? false) ? null : accesses[i].SubList?.Select(a => a.Clone()).ToArray()
            };
        }

        if (withSubList.Value)
        {
            result[accesses.Length - 1] = new EnumValueAccess
            {
                Value = "",
                Name = _enumSchema?.Cascade?[accesses.Length - 1],
                SubList = accesses.Last().SubList?.Select(a => a.Clone()).ToArray()
            };
        }
        
        return result;
    }

    /// <inheritdoc />
    public override bool IsAssignableTo(ValueType other)
    {
        return base.IsAssignableTo(other) || other is ScalarType scalar && _enumSchema?.Type switch
        {
            EnumValueType.String => scalar is StringType,
            EnumValueType.Int => scalar is IntType,
            EnumValueType.Flags => scalar is IntType,
            _ => false
        };
    }

    /// <inheritdoc />
    public override DataNode ParseValue(object? value)
    {
        return value is EnumNode node && node.Type == this ? node : _enumSchema?.Type switch
        {
            EnumValueType.Int or EnumValueType.Flags => new EnumNode(this, value?.TryConvertTo<long>()),
            _ => new EnumNode(this, value?.TryConvertTo<string>()),
        };
    }

    /// <inheritdoc />
    protected override async Task ValidateValueAsync(SchemaContext context, DataNode value)
    {
        if (value is not EnumNode result)
        {
            value.ViolatedConstraints = value.ViolatedConstraints != null
                ? value.ViolatedConstraints.Append(Kind).ToArray()
                : [Kind];
            return;
        }
        if (result.IsEmpty) return; // require is handled by struct, not here

        // Validate value
        if (_enumSchema?.Type == EnumValueType.Flags)
        {
            if (result.Value is not long flagsValue || flagsValue < 0 || flagsValue > _maxFlags)
            {
                result.ViolatedConstraints = [Kind];
            }
        }
        else
        {
            EnumValueSchema[] access = await LoadEnumValueAccessAsync(context, result.Value!.ToString());
            if (access.Length == 0)
            {
                result.ViolatedConstraints = [Kind];
            }
        }
    }

    /// <inheritdoc />
    public override bool IsIndexable => true;
    
    #endregion

    #region Utility

    /// <summary>
    /// Refresh status
    /// </summary>
    bool UpdateLoadState(EnumValueSchema node, int level = 999, EnumValueSchema? parent = null, bool reset = false)
    {
        if (node.IsFullyLoaded && !reset || level == 0) return true;
        node.IsFullyLoaded = false;
        _valueMaps[node.Value] = node;

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
        if (_enumSchema?.Type != EnumValueType.Flags || _root.SubList == null || _root.SubList.Length == 0) return;
        long max = 0;
        try
        {
            foreach (EnumValueSchema info in _root.SubList)
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

        _maxFlags = max * 2;
    }

    // Load the enum value access path
    async Task<EnumValueSchema[]> LoadEnumValueAccessAsync(SchemaContext context, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];

        // Try to get from cache
        if (GetAccess(value, out EnumValueSchema[]? accesses))
            return accesses ?? [];

        // Load from the provider
        if (Provider != null && context.GetRequiredService(Provider) is IEnumSchemaProvider provider)
        {
            EnumValueAccess[] accessList = await provider.LoadEnumAccessListAsync(Name, value, false, true);
            if (accessList.Length > 0)
            {
                lock (_lock)
                {
                    _root.CombineAccessList(accessList);
                    UpdateLoadState(_root);
                }
                return GetAccess(value, out accesses) ? accesses ?? [] : [];
            }
        }
        return [];

        bool GetAccess(string v, out EnumValueSchema[]? result)
        {
            result = null;
            if (!_valueMaps.TryGetValue(v, out var node)) return false;
            
            var temp = new EnumValueSchema[node.Level + 1];
            temp[node.Level] = node;
            for (int i = node.Level - 1; i >= 0; i--)
            {
                if (node.Parent == null) return false;
                node = node.Parent;
                temp[i] = node;
            }
            result = temp;
            return true;
        }
    }

    #endregion
}

public interface IEnumSchemaProvider: INodeSchemaProvider
{
    /// <summary>
    /// Load the enum value sub list
    /// </summary>
    /// <param name="schemaName">The enum schema name</param>
    /// <param name="value">The root enum value, optional</param>
    /// <param name="fullList">Whether load the full list</param>
    /// <returns></returns>
    Task<EnumValueSchema[]> LoadEnumSubListAsync(string schemaName, string? value, bool? fullList = null);
    
    /// <summary>
    /// Load the enum value access list from the server
    /// </summary>
    /// <param name="schemaName">The enum schema name</param>
    /// <param name="value">The enum value for access</param>
    /// <param name="noSubList">no sub list should be loaded</param>
    /// <param name="withSubList">with the value's sub list if existed</param>
    /// <returns></returns>
    Task<EnumValueAccess[]> LoadEnumAccessListAsync(string schemaName, string value, bool? noSubList = null, bool? withSubList = null);
}