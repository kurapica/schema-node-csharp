using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Schema;
using SchemaNode.Utility;
using SchemaNode.Property;
using SchemaNode.Struct;
using SchemaNode.Schema.Provider;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory enum schema representation
/// </summary>
public sealed class EnumType: ValueType
{
    #region Fields
    
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
        
        // Status
        if (_enumSchema == null) Error = ErrorCodes.NO_DEFINITION;
        UpdateMaxFlags();

        // Data
        _root = new Entry<string>();
        _root.SaveAccessList([
            new EntryAccess<string>
            {
                Children = _enumSchema?.Values ?? []
            }
        ]);
        
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
            EntryAccess<string>[] access = await GetEnumEntryAccess(context, strValue);
            if (access.Length == 0)
                result.SetViolated(Kind);
        }
    }

    /// <inheritdoc />
    public override bool IsIndexable => true;
    
    /// <summary>
    ///  Gets the enum value access path
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="value">The enum value to be queried</param>
    /// <param name="start">The start value of the access path</param>
    /// <returns></returns>
    public async Task<EntryAccess<string>[]> GetEnumEntryAccess(SchemaContext context, string? value, string? start = null)
    {
        if (string.IsNullOrWhiteSpace(start)) start = null;
        if (string.IsNullOrWhiteSpace(value)) value = null;

        Entry<string> root = (start != null ? _root.GetEntry(start) : null) ?? _root;

        EntryAccess<string>[]? access = root.GetAccessList(value);
        if (access is not null) return access;

        // Load from the provider
        if (Provider != null && context.GetRequiredService(Provider) is INodeSchemaProvider provider)
        {
            EntryAccess<string>[] accessList = await provider.GetEnumEntryAccess(Name, value, !root.IsRoot ? root.Value : null);
            if (accessList.Length > 0)
            {
                lock (_lock) root.SaveAccessList(accessList);
                return root.GetAccessList(value) ?? [];
            }
        }
        return [];
    }

    #endregion

    #region Utility

    void UpdateMaxFlags()
    {
        if (_enumSchema?.Type != EnumValueType.Flags) return;
        long max = 0;
        try
        {
            foreach (var info in _enumSchema.Values)
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

    #endregion
}
