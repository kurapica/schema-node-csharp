using SchemaNode.Attribute;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Schema;
using static SchemaNode.Utility.Constant;
using SchemaType = SchemaNode.Property.Schema.SchemaType;

namespace SchemaNode.Struct;

/// <summary>
/// The locale string
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_LOCALE_STRING)]
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
    /// </summary>
    [Meta<PrimaryIndex>]
    [Meta<UplimitStringProperty>(PRIMARY_KEY_MAX_LEN)]
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
        return new LocaleString(tuple.value, tuple.trans);
    }

    /// <summary>
    /// Tuple to locale string
    /// </summary>
    public static implicit operator LocaleString((string value, (string lang, string tran)[] trans) tuple)
    {
        return new LocaleString(tuple.value, tuple.trans);
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