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
    /// The max flags
    /// </summary>
    public long MaxFlags => _maxFlags;
    
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
        
        // max flags
        if (_enumSchema?.Type == EnumValueType.Flags)
        {
            _maxFlags = 0;
            foreach (var info in _enumSchema!.Values)
            {
                if (long.TryParse(info.Value, out long val))
                {
                    _maxFlags |= val;
                }
            }
        }

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
    public override T? GetProperty<T>() where T : class 
        => base.GetProperty<T>() ?? Runtime?.GetSchemaKindProperty<T>(Kind);

    /// <summary>
    /// Gets the properties with the given type
    /// </summary>
    public override IEnumerable<T> GetProperties<T>()
        => this.JoinProperties(base.GetProperties<T>(), Runtime?.GetSchemaKindProperties<T>(Kind));

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
    public override bool IsIndexable => true;
    
    /// <summary>
    ///  Gets the enum value access path
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="value">The enum value to be queried</param>
    /// <param name="start">The start value of the access path</param>
    /// <returns></returns>
    public async Task<EntryAccess<string>[]> GetEnumEntryAccessAsync(SchemaContext context, string? value, string? start = null)
    {
        if (string.IsNullOrWhiteSpace(start)) start = null;
        if (string.IsNullOrWhiteSpace(value)) value = null;

        Entry<string>? root = (start != null ? _root.GetEntry(start) : null) ?? _root;

        EntryAccess<string>[]? access = root.GetAccessList(value); // if value is null, always has return value
        if (access is not null || root.IsFullyLoaded == true) return access ?? [];

        // Load from the provider
        if (Provider != null && context.GetRequiredService(Provider) is IEnumEntryProvider provider)
        {
            EntryAccess<string>[] accessList = await provider.GetEnumEntryAccessAsync(Name, value, !root.IsRoot ? root.Value : null);
            if (accessList.Length > 0)
            {
                lock (_lock) root.SaveAccessList(accessList);
                if (start != null && root.IsRoot)
                {
                    root = root.GetEntry(start);
                    if (root is null) return []; // strange start point
                }
                return root.GetAccessList(value) ?? [];
            }
        }
        return [];
    }

    #endregion
}
