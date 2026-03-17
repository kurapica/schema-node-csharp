using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Enum;
using System.ComponentModel.DataAnnotations;
using static SchemaNode.Utility.Constant;
using System.ComponentModel.DataAnnotations.Schema;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Schema;

/// <summary>
/// The data node schema
/// The schema is used to describe the data node
/// </summary>
[SchemaApp]
[Schema($"{NS_SYSTEM_SCHEMA_DEF}.{nameof(NodeSchema)}")]
public sealed class NodeSchema
{
    /// <summary>
    /// The parent schema name
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    [Index("IX_SUB_NS")]
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// The schema name
    /// </summary>
    [Index]
    [Index("IX_SUB_NS", 1)]
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The schema type
    /// </summary>
    public SchemaType Type { get; set; } = SchemaType.Namespace;

    /// <summary>
    /// The schema display
    /// </summary>
    public LocaleString? Display { get; set; }

    /// <summary>
    /// The authentication policy type
    /// </summary>
    [Schema(NS_SYSTEM_SCHEMA_TYPE_POLICY)]
    public string? Auth { get; set; }

    /// <summary>
    /// The scalar schema if type is scalar
    /// </summary>
    [NotMapped]
    public ScalarSchema? Scalar { get; set; }

    /// <summary>
    /// The enum schema if type is enum
    /// </summary>
    [NotMapped]
    public EnumSchema? Enum  { get; set; }

    /// <summary>
    /// The struct schema if type is struct
    /// </summary>
    [NotMapped]
    public StructSchema? Struct { get; set; }

    /// <summary>
    /// The array schema if type is array
    /// </summary>
    [NotMapped]
    public ArraySchema? Array  { get; set; }

    /// <summary>
    /// The function schema if type is function
    /// </summary>
    [NotMapped]
    public FunctionSchema? Func { get; set; }
    
    /// <summary>
    /// The event schema if type is event
    /// </summary>
    [NotMapped]
    public EventSchema? Event  { get; set; }
    
    /// <summary>
    /// The workflow schema if type is workflow
    /// </summary>
    [NotMapped]
    public WorkflowSchema? Workflow  { get; set; }
    
    /// <summary>
    /// The permission policy schema
    /// </summary>
    [NotMapped]
    public PolicySchema? Policy  { get; set; }
    
    /// <summary>
    /// The load state
    /// </summary>
    [NotMapped]
    public SchemaLoadState? LoadState
    {
        get => _schemaLoadState;
        set
        {
            _schemaLoadState = value;
            if (Schemas == null) return;
            foreach (NodeSchema schema in Schemas)
            {
                schema.LoadState = value;
            }
        }
    }

    /// <summary>
    /// Has sub schemas
    /// </summary>
    [NotMapped]
    public bool? HasSchemas { get; set; }
    
    /// <summary>
    /// The schema is used, can't be deleted
    /// </summary>
    [NotMapped]
    public bool? Used { get; set; }
    
    /// <summary>
    /// Used by other types
    /// </summary>
    [NotMapped]
    public string[]? UsedBy { get; set; }
    
    /// <summary>
    /// Used by other apps
    /// </summary>
    [NotMapped]
    public string[]? UsedByApp { get; set; }

    /// <summary>
    /// The sub schemas of the namespace
    /// </summary>
    [NotMapped]
    public NodeSchema[]? Schemas  { get; set; }
    
    /// <summary>
    /// The compatible schemas
    /// </summary>
    [NotMapped]
    public CompatibleSchema[]? Compatibles { get; set; }

    /// <summary>
    /// The schema provider used to fetch the node schema
    /// </summary>
    [JsonIgnore]
    [NotMapped]
    public Type? SchemaProvider
    {
        get => _schemaProvider;
        set
        {
            _schemaProvider = value;
            if (Schemas == null) return;
            foreach (NodeSchema schema in Schemas)
                schema.SchemaProvider = value;
        }
    }
    
    /// <summary>
    /// The schema node status
    /// </summary>
    [NotMapped]
    public SchemaNodeStatus? Status { get; set; }

    #region Methods

    internal NodeSchema With(NodeSchema[] schemas)
    {
        Schemas = schemas;
        return this;
    }

    internal NodeSchema With(ScalarSchema scalar)
    {
        if (Type != SchemaType.Scalar) return this;
        Scalar = scalar;
        return this;
    }

    internal NodeSchema With(EnumSchema enumSchema)
    {
        if (Type != SchemaType.Enum) return this;
        Enum = enumSchema;
        return this;
    }

    internal NodeSchema With(StructSchema structSchema)
    {
        if (Type != SchemaType.Struct) return this;
        Struct = structSchema;
        return this;
    }

    internal NodeSchema With(ArraySchema arraySchema)
    {
        if (Type != SchemaType.Array) return this;
        Array = arraySchema;
        return this;
    }

    internal NodeSchema With(FunctionSchema functionSchema)
    {
        if (Type != SchemaType.Func) return this;
        Func = functionSchema;
        return this;
    }

    internal NodeSchema With(EventSchema eventSchema)
    {
        if (Type != SchemaType.Event) return this;
        Event = eventSchema;
        return this;
    }

    internal NodeSchema With(WorkflowSchema workflowSchema)
    {
        if (Type != SchemaType.Workflow) return this;
        Workflow = workflowSchema;
        return this;
    }

    internal NodeSchema With(PolicySchema policySchema)
    {
        if (Type != SchemaType.Policy) return this;
        Policy = policySchema;
        return this;
    }

    /// <summary>
    /// Used to combine custom schema to system schema
    /// </summary>
    internal void CombineCustomSchema(NodeSchema? other)
    {
        if (other == null || other.Type != Type) return;

        Display = Display != null ? Display.Concat(other.Display) : other.Display;
        Auth = string.IsNullOrWhiteSpace(other.Auth) ? Auth : other.Auth;

        switch (Type)
        {
            case SchemaType.Scalar:
                Scalar?.CombineCustomSchema(other.Scalar);
                break;
            case SchemaType.Enum:
                Enum?.CombineCustomSchema(other.Enum);
                break;
            case SchemaType.Array:
                Array?.CombineCustomSchema(other.Array);
                break;
            case SchemaType.Struct:
                Struct?.CombineCustomSchema(other.Struct);
                break;
        }
    }

    #endregion

    #region Utility

    private SchemaLoadState? _schemaLoadState;
    private Type? _schemaProvider;

    #endregion
}

/// <summary>
/// The locale translate
/// </summary>
[Schema(NS_SYSTEM_LOCALE_TRAN)]
public sealed class LocaleTran
{
    /// <summary>
    /// default constructor
    /// </summary>
    public LocaleTran(){}
    
    /// <summary>
    /// The locale translate
    /// </summary>
    public LocaleTran(string lang, string? tran)
    {
        Lang = lang;
        Tran = tran;
    }

    /// <summary>
    /// The language
    /// </summary>
    [Schema(NS_SYSTEM_LANGUAGE)]
    [MaxLength(8)]
    [Index]
    public string Lang { get; set; } = string.Empty;

    /// <summary>
    /// The translation
    /// </summary>
    public string? Tran { get; set; }
    
    /// <summary>
    /// Convert tuple to locale translate
    /// </summary>
    public static implicit operator LocaleTran((string lang, string tran) tuple)
    {
        return new LocaleTran(tuple.lang, tuple.tran);
    }
}

/// <summary>
/// The locale string
/// </summary>
[Schema(NS_SYSTEM_LOCALE_STRING)]
public sealed class LocaleString : ICloneable
{
    /// <summary>
    /// default constructor
    /// </summary>
    public LocaleString()
    {
    }
    
    /// <summary>
    /// The locale string
    /// </summary>
    public LocaleString(string key, LocaleTran[] trans)
    {
        Key = key;
        Trans = trans;
    }

    public LocaleString(string key, params (string lang, string tran)[]? trans)
    {
        Key = key;
        Trans = trans?.Select(t => new LocaleTran(t.lang, t.tran)).ToArray();
    }

    /// <summary>
    /// The default key
    /// If key is like '{list.prefix}{@schema.path}{list.suffix}', it means to use the schema path to translate and global string for other part
    /// It has no translation record
    /// {list.prefix} - global strings
    /// {@schema.path} - use schema path to translate, default display
    /// {#appschema.path} - use app schema path to translate
    /// </summary>
    [Index]
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// The translations
    /// </summary>
    public LocaleTran[]? Trans { get; set; }

    /// <summary>
    /// Convert string to locale string
    /// </summary>
    public static implicit operator LocaleString(string? value)
    {
        return new LocaleString(value ?? string.Empty);
    }
    
    /// <summary>
    /// Tuple to locale string
    /// </summary>
    public static implicit operator LocaleString((string value, (string lang, string tran) trans) tuple)
    {
        return new  LocaleString(tuple.value, tuple.trans);
    }

    /// <summary>
    /// Tuple to locale string
    /// </summary>
    public static implicit operator LocaleString((string value, (string lang, string tran)[] trans) tuple)
    {
        return new  LocaleString(tuple.value, tuple.trans);
    }

    /// <summary>
    /// Convert locale string to string
    /// </summary>
    public static implicit operator string(LocaleString locale)
    {
        return locale.Key;
    }

    /// <summary>
    /// Clone the locale string
    /// </summary>
    public object Clone()
    {
        return new LocaleString(Key, Trans?.Select(t => new LocaleTran(t.Lang, t.Tran)).ToArray() ?? []);
    }

    /// <summary>
    /// To string
    /// </summary>
    /// <returns></returns>
    public override string ToString() => Key;

    public LocaleString Concat(LocaleString? other)
    {
        if (other == null) return this;
        Key = string.IsNullOrWhiteSpace(other.Key) ? Key : other.Key;

        // Combine trans
        if (Trans == null || Trans.Length == 0)
            Trans = other.Trans;
        else if (other.Trans is { Length: > 0 })
        {
            foreach (LocaleTran tran in Trans)
            {
                var inOther = other.Trans.FirstOrDefault(t => t.Lang.Equals(tran.Lang, StringComparison.OrdinalIgnoreCase));
                if (inOther != null)
                {
                    tran.Tran = string.IsNullOrWhiteSpace(inOther.Tran) ? tran.Tran : inOther.Tran;
                }
            }
            var otherOnly = other.Trans.Where(t => !Trans.Any(a => a.Lang.Equals(t.Lang, StringComparison.OrdinalIgnoreCase))).ToArray();
            if (otherOnly is { Length: > 0 })
                Trans = Trans.Concat(otherOnly).ToArray();
        }

        return this;
    }
}

/// <summary>
/// The dict entry
/// </summary>
[Schema(NS_SYSTEM_ENTRY)]
public sealed class Entry
{
    /// <summary>
    /// The entry value
    /// </summary>
    [Index]
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// The entry label
    /// </summary>
    public LocaleString? Label { get; set; }
    
    /// <summary>
    /// The entry children
    /// </summary>
    public Entry[]? Children { get; set; }
}

/// <summary>
/// The compatible schema record
/// </summary>
/// <param name="To">The compatible type</param>
/// <param name="Convert">The convert function</param>
public sealed record CompatibleSchema(string To, string Convert);