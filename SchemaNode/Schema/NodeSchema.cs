using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Enum;
using System.ComponentModel.DataAnnotations;
using static SchemaNode.Utility.Constant;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchemaNode.Schema;

/// <summary>
/// The data node schema
/// The schema is used to describe the data node
/// </summary>
[SchemaApp]
public class NodeSchema
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

    #region Methods

    internal NodeSchema WithSchemas(NodeSchema[] schemas)
    {
        Schemas = schemas;
        return this;
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
[SchemaType(NS_SYSTEM_LOCALE_TRAN)]
public class LocaleTran
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
    [SchemaType(NS_SYSTEM_LANGUAGE)]
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
[SchemaType(NS_SYSTEM_LOCALE_STRING)]
public class LocaleString : ICloneable
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
    /// Clone the locale string
    /// </summary>
    public object Clone()
    {
        return new LocaleString(Key, Trans?.Select(t => new LocaleTran(t.Lang, t.Tran)).ToArray() ?? []);
    }
}

/// <summary>
/// The dict entry
/// </summary>
[SchemaType(NS_SYSTEM_ENTRY)]
public class Entry
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
}