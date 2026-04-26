using SchemaNode.Context;
using SchemaNode.Struct;
using System.Text.Json;

namespace SchemaNode.Utility;

/// <summary>
/// Provides system-level locale translations loaded from locale JSON files in the output directory.
/// </summary>
internal static class Locale
{
    // useful global string
    public const string LIST_PREFIX = "{[LIST.PREFIX]}";
    public const string LIST_SUFFIX = "{[LIST.SUFFIX]}";
    
    // locale code → (schema key → translated text)
    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _locales
        = new Dictionary<string, IReadOnlyDictionary<string, string>>();

    /// <summary>
    /// Whether any locale data has been loaded
    /// </summary>
    public static bool HasLocales => _locales.Count > 0;

    /// <summary>
    /// Tries to load locale JSON files from the given directory (defaults to {BaseDirectory}/locale).
    /// Each file must be named after its locale code, e.g. enUS.json, zhCN.json.
    /// Safe to call multiple times; each call replaces the previously loaded data.
    /// </summary>
    internal static void TryLoad(string? directory = null)
    {
        directory ??= Path.Combine(AppContext.BaseDirectory, "locale");
        if (!Directory.Exists(directory)) return;

        var loaded = new Dictionary<string, IReadOnlyDictionary<string, string>>();
        foreach (string file in Directory.GetFiles(directory, "*.json"))
        {
            string locale = Path.GetFileNameWithoutExtension(file);
            try
            {
                string json = File.ReadAllText(file, System.Text.Encoding.UTF8);
                Dictionary<string, string>? dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (dict is { Count: > 0 })
                    loaded[locale] = dict;
            }
            catch
            {
                // ignore unreadable or malformed files
            }
        }
        _locales = loaded;
    }

    /// <summary>
    /// Returns the translated string for <paramref name="key"/> in the given <paramref name="locale"/>,
    /// or <see langword="null"/> when the key or locale is not found.
    /// </summary>
    public static string? GetString(string key, string? locale)
    {
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(locale)) return null;
        if (_locales.TryGetValue(locale, out IReadOnlyDictionary<string, string>? dict)
            && dict.TryGetValue(key, out string? value))
            return value;
        return null;
    }

    /// <summary>
    /// Gets the list of available locale codes loaded from JSON files, e.g. "enUS", "zhCN".
    /// </summary>
    public static IEnumerable<string> GetAvailableLocales(this SchemaContext context) => _locales.Keys;

    /// <summary>
    /// Supplements the <see cref="LocaleString.Trans"/> array with translations found in the loaded
    /// locale dictionaries.
    /// <see cref="LocaleString.Key"/>) as the lookup key.
    /// The original <see cref="LocaleString.Key"/> is never modified.
    /// </summary>
    internal static void Translate(LocaleString? localeString, string? lookupKey = null)
    {
        if (localeString == null || _locales.Count == 0) return;

        lookupKey ??= localeString.Key;
        if (string.IsNullOrEmpty(lookupKey)) return;

        List<LocaleTran>? trans = null;
        foreach ((string locale, IReadOnlyDictionary<string, string> dict) in _locales)
        {
            if (!dict.TryGetValue(lookupKey, out string? value) || string.IsNullOrEmpty(value))
                continue;

            trans ??= [.. (localeString.Trans ?? [])];
            if (!trans.Any(t => t.Lang == locale))
                trans.Add(new LocaleTran(locale, value));
        }

        if (trans != null)
            localeString.Trans = [.. trans];
    }
}
