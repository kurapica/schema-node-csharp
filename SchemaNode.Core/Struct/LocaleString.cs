using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.String;
using SchemaNode.Runtime;
using SchemaNode.Utility;
using System.Text.RegularExpressions;
using static SchemaNode.Utility.Constant;
using SchemaType = SchemaNode.Property.Core.SchemaType;

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
    [Meta<UpLimitString>(PRIMARY_KEY_MAX_LEN)]
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

    /// <summary>
    /// Concat other locale string
    /// </summary>
    public LocaleString Concat(LocaleString? other)
    {
        if (other == null) return this;
        Key = string.IsNullOrWhiteSpace(other.Key) ? Key : other.Key;

        // CombineProperties trans
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

    public string ToString(ISchemaContext context)
    {
        var ctx = context as SchemaContext;
        var locale = !string.IsNullOrWhiteSpace(ctx?.Locale) ? ctx.Locale.Replace("-", "") : null;

        string result = (locale != null && Trans?.FirstOrDefault(t => t.Lang.StartsWith(locale, StringComparison.OrdinalIgnoreCase) || locale.StartsWith(t.Lang, StringComparison.OrdinalIgnoreCase)) is { } tran 
            ? tran.Tran : null) ?? Key;

        if (result.IndexOf("{") >= 0)
        {
            var convert = Regex.Replace(result, @"\{([^}]+)\}", match =>
            {
                string replace = match.Groups[1].Value;
                if (Regex.IsMatch(replace, @"^\d+$")) return match.Groups[0].Value; // ignore {0}, {1}, etc.
                if (replace.StartsWith("@"))
                {
                    string key = replace.Substring(1);
                    string field = "";
                    if (key.IndexOf(':') >= 0)
                    {
                        var parts = key.Split(':', 2);
                        key = parts[0];
                        field = parts[1];
                    }
                    var type = ctx?.GetNodeTypeAsync(key).GetAwaiter().GetResult();
                    if (type != null)
                    {
                        if (!string.IsNullOrWhiteSpace(field))
                        {
                            var f = (type as IValueTypeAccess)?.GetAccessValueType(field);
                            var display = f?.GetProperty<Display>()?.Value;
                            return display != null ? display.ToString(context) : f?.Name ?? field;
                        }
                        else
                        {
                            var display = type.GetProperty<Display>()?.Value;
                            return display != null ? display.ToString(context) : type.Name;
                        }
                    }
                    return Locale.GetString(key, locale) ?? replace;
                }
                return Locale.GetString(replace, locale) ?? replace;
            });
        }
        return result;
    }

    /// <summary>
    /// Format the locale string with arguments.
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    public string Format(ISchemaContext context, params object[] args)
    {
        return Regex.Replace(ToString(context), @"\{(\d+)\}", match =>
        {
            int index = int.Parse(match.Groups[1].Value);
            if (index >= 0 && index < args.Length)
                return args[index] is LocaleString locale ? locale.ToString(context) : args[index]?.ToString() ?? string.Empty;
            return match.Groups[0].Value; // Return the original placeholder if index is out of range
        });
    }
}