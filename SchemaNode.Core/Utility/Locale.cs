using SchemaNode.Context;
using SchemaNode.Struct;
using System.Reflection;
using System.Text.Json;

namespace SchemaNode.Utility;

/// <summary>
/// Provides system-level locale translations loaded from built-in locale JSON files
/// embedded in the schema assemblies, optionally overridden by files in the output directory.
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
    /// Tries to load locale JSON files.
    /// Built-in locale files embedded as resources in <paramref name="assemblies"/> are read first,
    /// then any JSON files in <paramref name="directory"/> (defaults to {BaseDirectory}/locale)
    /// are merged on top, so directory files override the embedded built-ins.
    /// Embedded resources must follow the naming convention <c>schema_*_&lt;locale&gt;.json</c>
    /// (e.g. <c>schema_core_enUS.json</c>); directory files may also use a bare <c>&lt;locale&gt;.json</c> name.
    /// Safe to call multiple times; each call replaces the previously loaded data.
    /// </summary>
    internal static void TryLoad(IEnumerable<Assembly>? assemblies = null, string? directory = null)
    {
        // locale code → (schema key → translated text). Later merges overwrite earlier ones,
        // so directory files take precedence over embedded built-ins.
        var loaded = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        // 1. Built-in locale files embedded in the given assemblies
        if (assemblies is not null)
        {
            foreach (Assembly assembly in assemblies)
            {
                foreach (string name in assembly.GetManifestResourceNames())
                {
                    string? locale = GetEmbeddedLocale(name);
                    if (locale is null) continue;
                    try
                    {
                        using Stream? stream = assembly.GetManifestResourceStream(name);
                        if (stream is null) continue;
                        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
                        Merge(reader.ReadToEnd(), locale, loaded);
                    }
                    catch
                    {
                        // ignore unreadable or malformed embedded resources
                    }
                }
            }
        }

        // 2. Locale JSON files in the output directory (overrides the embedded built-ins)
        directory ??= Path.Combine(AppContext.BaseDirectory, "locale");
        if (Directory.Exists(directory))
        {
            foreach (string file in Directory.GetFiles(directory, "*.json"))
            {
                string? locale = GetFileLocale(file);
                if (locale is null) continue;
                try
                {
                    Merge(File.ReadAllText(file, System.Text.Encoding.UTF8), locale, loaded);
                }
                catch
                {
                    // ignore unreadable or malformed files
                }
            }
        }

        _locales = loaded.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyDictionary<string, string>)kv.Value,
            StringComparer.Ordinal);
    }

    /// <summary>
    /// Extracts the locale code from a manifest resource name such as
    /// <c>SchemaNode.locale.schema_core_enUS.json</c>. Only resources whose file stem
    /// starts with <c>schema_</c> are recognized, to avoid picking up unrelated resources.
    /// </summary>
    private static string? GetEmbeddedLocale(string resourceName)
    {
        ReadOnlySpan<char> span = resourceName.AsSpan();
        if (!span.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) return null;
        span = span[..^5]; // strip ".json"

        // The file stem is the segment after the last '.' (resource namespace separator)
        int lastDot = span.LastIndexOf('.');
        string stem = lastDot >= 0 ? span[(lastDot + 1)..].ToString() : span.ToString();
        return GetLocaleFromStem(stem);
    }

    /// <summary>
    /// Extracts the locale code from a file path such as
    /// <c>schema_core_enUS.json</c> or <c>enUS.json</c>.
    /// </summary>
    private static string? GetFileLocale(string path)
    {
        string stem = Path.GetFileNameWithoutExtension(path);
        return GetLocaleFromStem(stem);
    }

    /// <summary>
    /// Extracts the locale code from a file stem. When <paramref name="requireSchemaPrefix"/>
    /// is <see langword="true"/>, only stems starting with <c>schema_</c> are accepted.
    /// </summary>
    private static string? GetLocaleFromStem(string stem)
    {
        int lastUnderscore = stem.LastIndexOf('_');
        if (lastUnderscore < 0) return stem; // bare locale name, e.g. "enUS"
        if (lastUnderscore == stem.Length - 1) return null; // trailing underscore
        return stem[(lastUnderscore + 1)..];
    }

    /// <summary>
    /// Deserializes a locale JSON object and merges its entries into <paramref name="loaded"/>
    /// for the given <paramref name="locale"/>. Later values overwrite earlier ones.
    /// </summary>
    private static void Merge(string json, string locale, Dictionary<string, Dictionary<string, string>> loaded)
    {
        Dictionary<string, string>? dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        if (dict is null || dict.Count == 0) return;
        if (!loaded.TryGetValue(locale, out Dictionary<string, string>? target))
            loaded[locale] = target = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> pair in dict)
            target[pair.Key] = pair.Value;
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
