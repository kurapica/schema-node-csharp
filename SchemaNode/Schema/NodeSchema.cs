using SchemaNode.Enum;

namespace SchemaNode.Schema;

/// <summary>
/// The data node schema
/// The schema is used to describe the data node
/// </summary>
public class NodeSchema
{
    /// <summary>
    /// The schema name
    /// </summary>
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
    public ScalarSchema? Scalar { get; set; }

    /// <summary>
    /// The enum schema if type is enum
    /// </summary>
    public EnumSchema? Enum  { get; set; }

    /// <summary>
    /// The struct schema if type is struct
    /// </summary>
    public StructSchema? Struct { get; set; }

    /// <summary>
    /// The array schema if type is array
    /// </summary>
    public ArraySchema? Array  { get; set; }

    /// <summary>
    /// The function schema if type is function
    /// </summary>
    public FunctionSchema? Func { get; set; }
    
    /// <summary>
    /// The load state
    /// </summary>
    public SchemaLoadState? LoadState { get; set; }

    /// <summary>
    /// The sub schemas of the namespace
    /// </summary>
    public NodeSchema[]? Schemas  { get; set; }
}

/// <summary>
/// The locale translate
/// </summary>
/// <param name="Lang">Language</param>
/// <param name="Tran">Translate</param>
public record LocaleTran(string Lang, string Tran);

/// <summary>
/// The locale string
/// </summary>
public class LocaleString: ICloneable
{
    /// <summary>
    /// The default key
    /// </summary>
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