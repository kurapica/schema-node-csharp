using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Collections.Concurrent;
using SchemaNode.Property;
using SchemaNode.Schema.Provider;
using SchemaNode.Struct;

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
    private Entry<string> _root = new();

    #endregion
    
    #region Properties
    
    /// <summary>
    /// The enum value type
    /// </summary>
    public EnumValueType Type => _enumSchema?.Type ?? EnumValueType.String;
    
    /// <summary>
    /// The enum cascade
    /// </summary>
    public LocaleString[]? Cascade => _enumSchema?.Cascade;

    /// <inheritdoc/>
    public override Type? GetCsharpType() => base.GetCsharpType() ?? Type switch
    {
        EnumValueType.String => typeof(string),
        EnumValueType.Int => typeof(long),
        EnumValueType.Flags => typeof(long),
        _ => null
    };

    #endregion
    
    #region Method

    /// <inheritdoc />
    public override Task LoadAsync(SchemaContext context)
    {
        _enumSchema = GetProperty<EnumProperty>()?.Value;

        // Data
        _root = new Entry<string>();
        _root.SaveAccessList([
            new EntryAccess<string>
            {
                Children = _enumSchema?.Values ?? []
            }
        ]);
        UpdateMaxFlags();
        
        // Status
        if (_enumSchema == null) Error = ErrorCodes.NO_DEFINITION;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the property with the given type
    /// </summary>
    public new T? GetProperty<T>() where T : class, IProperty => base.GetProperty<T>() ?? Runtime?.GetSchemaKindProperty<T>(Kind);

    /// <summary>
    /// Gets the properties with the given type
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public new IEnumerable<T> GetProperties<T>() where T : class, IProperty
    {
        foreach (var property in base.GetProperties<T>())
        {
            yield return property;
            if (!property.Stackable) yield break;
        }

        if (Runtime != null)
        {
            foreach (T property in Runtime.GetSchemaKindProperties<T>(Kind))
            {
                yield return property;
                if (!property.Stackable) yield break;
            }
        }
    }

    /// <summary>
    /// Load the enum value sub list
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="value">The root enum value, optional</param>
    /// <returns></returns>
    public async Task<EnumValueSchema[]> LoadEnumSubListAsync(SchemaContext context, string? value)
    {
        bool fullList = false;
        if (string.IsNullOrWhiteSpace(value))
            return _root.Clone(fullList ? (_enumSchema?.Cascade?.Length ?? 1) : 1).Children ?? [];

        EnumValueSchema[] accesses = await LoadEnumValueAccessAsync(context, value);
        if (accesses.Length == 0) return [];

        EnumValueSchema access = accesses.Last();
        if (!(access.HasChildren ?? false)) return [];

        // load sub list
        int chkLvl = 1;
        if (fullList)
            chkLvl = Math.Min((_enumSchema?.Cascade?.Length ?? 1) - accesses.Length + 1, MAX_SUBLIST_LEVEL);

        // full-filled
        if (UpdateLoadState(access, chkLvl))
            return access.Clone(chkLvl).Children ?? [];

        // load sub list
        if (Provider != null && context.GetRequiredService(Provider) is IEnumSchemaProvider provider)
        {
            EnumValueSchema[] subList = await provider.LoadEnumSubListAsync(Name, value);
            lock (_lock)
            {
                access.Children = subList;
                UpdateLoadState(access);
            }
            return access.Clone(chkLvl).Children ?? [];
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
        
        withSubList = (withSubList ?? false) && accesses.Length < (_enumSchema?.Cascade?.Length ?? 1) && accesses.Last().Children is { Length: > 0};
        EnumValueAccess[] result = new EnumValueAccess[withSubList.Value ? accesses.Length : (accesses.Length - 1)];
        for (int i = 0; i < accesses.Length - 1; i++)
        {
            result[i] = new EnumValueAccess
            {
                Value = accesses[i + 1].Value,
                Name = _enumSchema?.Cascade?[i],
                Schema = noSubList == true ? accesses[i + 1].Clone() : null,
                SubList = (noSubList ?? false) ? null : accesses[i].Children?.Select(a => a.Clone()).ToArray()
            };
        }

        if (withSubList.Value)
        {
            result[accesses.Length - 1] = new EnumValueAccess
            {
                Value = "",
                Name = _enumSchema?.Cascade?[accesses.Length - 1],
                SubList = accesses.Last().Children?.Select(a => a.Clone()).ToArray()
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
    public override DataNode Create(IValueAccess? parent = null) => new EnumNode(this,  parent);

    /// <inheritdoc />
    protected override async Task ValidateNodeAsync(SchemaContext context, DataNode value)
    {
        if (value is not EnumNode result || result.Type != this || result.IsEmpty) return;

        // Validate value
        if (_enumSchema?.Type == EnumValueType.Flags)
        {
            if (!result.TryGetValue(out long flagsValue) || flagsValue < 0 || flagsValue > _maxFlags)
                result.SetViolated(Kind);
        }
        else if (result.TryGetValue(out string? strValue))
        {
            EnumValueSchema[] access = await LoadEnumValueAccessAsync(context, strValue);
            if (access.Length == 0)
                result.SetViolated(Kind);
        }
    }

    /// <inheritdoc />
    public override bool IsIndexable => true;
    
    #endregion

    #region Utility

    void UpdateMaxFlags()
    {
        if (_enumSchema?.Type != EnumValueType.Flags || _root.Children == null || _root.Children.Length == 0) return;
        long max = 0;
        try
        {
            foreach (var info in _root.Children)
            {
                if (long.TryParse(info.Value, out long val))
                {
                    max |= val;
                }
            }
        }
        catch
        {
            // pass
        }

        _maxFlags = max;
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
