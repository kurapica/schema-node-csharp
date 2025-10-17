using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Enum;
using System.ComponentModel.DataAnnotations;
using static SchemaNode.Utility.Constant;
using System.Reflection.Metadata;

namespace SchemaNode.Schema;

/// <summary>
/// The data node schema
/// The schema is used to describe the data node
/// </summary>
[SchemaStruct([nameof(Name)], [nameof(Namespace), nameof(Name)])]
[SchemaApp]
public class NodeSchema
{
    /// <summary>
    /// The parent schema name
    /// </summary>
    [MaxLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string Namespace { get; set; } = string.Empty;
    
    /// <summary>
    /// The schema name
    /// </summary>
    [MaxLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
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
    [SchemaStructMemIgnore]
    public ScalarSchema? Scalar { get; set; }

    /// <summary>
    /// The enum schema if type is enum
    /// </summary>
    [SchemaStructMemIgnore]
    public EnumSchema? Enum  { get; set; }

    /// <summary>
    /// The struct schema if type is struct
    /// </summary>
    [SchemaStructMemIgnore]
    public StructSchema? Struct { get; set; }

    /// <summary>
    /// The array schema if type is array
    /// </summary>
    [SchemaStructMemIgnore]
    public ArraySchema? Array  { get; set; }

    /// <summary>
    /// The function schema if type is function
    /// </summary>
    [SchemaStructMemIgnore]
    public FunctionSchema? Func { get; set; }

    /// <summary>
    /// The load state
    /// </summary>
    [SchemaStructMemIgnore]
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
    [SchemaStructMemIgnore]
    public bool? HasSchemas { get; set; }
    
    /// <summary>
    /// The schema is used, can't be deleted
    /// </summary>
    [SchemaStructMemIgnore]
    public bool? Used { get; set; }
    
    /// <summary>
    /// Used by other types
    /// </summary>
    [SchemaStructMemIgnore]
    public string[]? UsedBy { get; set; }
    
    /// <summary>
    /// Used by other apps
    /// </summary>
    [SchemaStructMemIgnore]
    public string[]? UsedByApp { get; set; }

    /// <summary>
    /// The sub schemas of the namespace
    /// </summary>
    [SchemaStructMemIgnore]
    public NodeSchema[]? Schemas  { get; set; }

    /// <summary>
    /// The schema provider used to fetch the node schema
    /// </summary>
    [JsonIgnore]
    [SchemaStructMemIgnore]
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

    #region Utility

    private SchemaLoadState? _schemaLoadState;
    private Type? _schemaProvider;

    #endregion
}

/// <summary>
/// The locale translate
/// </summary>
/// <param name="Lang">Language</param>
/// <param name="Tran">Translate</param>
[SchemaNameSpace(NS_SYSTEM_LOCALE_TRAN)]
[SchemaStruct([nameof(Lang)])]
public class LocaleTran
{
    /// <summary>
    /// The language
    /// </summary>
    [SchemaStructMem(type: NS_SYSTEM_LANGUAGE)]
    public required string Lang { get; set; }

    /// <summary>
    /// The translation
    /// </summary>
    public string? Tran { get; set; }
}

/// <summary>
/// The locale string
/// </summary>
[SchemaNameSpace(NS_SYSTEM_LOCALE_STRING)]
[SchemaStruct([nameof(Key)])]
public class LocaleString: ICloneable
{
    /// <summary>
    /// The default key
    /// </summary>
    [MaxLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
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
        return new LocaleString
        {
            Key = value ?? string.Empty,
        };
    }

    public object Clone()
    {
        return new LocaleString
        {
            Key = Key,
            Trans = Trans?.ToArray(),
        };
    }
}

[SchemaNameSpace(NS_SYSTEM_ENTRY)]
[SchemaStruct([nameof(Value)])]
public class Entry
{
    public string Value { get; set; } = string.Empty;

    public LocaleString? Label { get; set; }
}