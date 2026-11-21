using System.ComponentModel;
using System.Globalization;
using SchemaNode.Utility;

namespace SchemaNode.Avalonia.View.Localization;

/// <summary>
/// The single UI-side entry point for localized text.
///
/// It resolves the same keys as the Core locale dictionaries through <see cref="Locale.GetString"/>
/// (no private dictionary is kept in the view layer). <see cref="Version"/> is raised whenever the
/// current locale changes, so bindings using <see cref="LocalizedTextConverter"/> can refresh in
/// place without rebuilding the visual tree.
/// </summary>
public sealed class LocaleManager : INotifyPropertyChanged
{
    /// <summary>Simplified Chinese locale code used by Core and App locale files</summary>
    public const string ZH_CN = "zhCN";

    /// <summary>English locale code used by Core and App locale files</summary>
    public const string EN_US = "enUS";

    /// <summary>Process-wide instance shared by the Man and View layers</summary>
    public static LocaleManager Instance { get; } = new LocaleManager();

    private string _currentLocale = ZH_CN;
    private int _version;

    private LocaleManager()
    {
        // This assembly is not a schema assembly, so the Core startup load never scans it; merge
        // the view-owned texts (avalonia.*) into the shared dictionaries on first use. Existing
        // entries from Core, the host or the locale directory keep precedence.
        Locale.MergeAssembly(typeof(LocaleManager).Assembly);
    }

    /// <summary>Current locale code, e.g. "zhCN" or "enUS"</summary>
    public string CurrentLocale
    {
        get => _currentLocale;
        private set
        {
            if (_currentLocale.Equals(value, StringComparison.OrdinalIgnoreCase)) return;
            _currentLocale = value;
            _version++;
            OnPropertyChanged(nameof(CurrentLocale));
            OnPropertyChanged(nameof(Version));
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Monotonic counter used as the binding source. Any binding on this property is re-evaluated
    /// when <see cref="CurrentLocale"/> changes, which is the refresh strategy for text bindings.
    /// </summary>
    public int Version => _version;

    /// <summary>Raised after the current locale has changed and all bindings were notified</summary>
    public event EventHandler? LanguageChanged;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets the translation for <paramref name="key"/> in the current locale.
    /// A missing key or locale falls back to the key itself, so missing entries stay visible.
    /// </summary>
    public string Get(string key) => Locale.GetString(key, _currentLocale) ?? key;

    /// <summary>
    /// Gets the translation for <paramref name="key"/> only when the current locale defines it.
    /// Callers that must not display an untranslated key (raw status codes, for example) use this
    /// instead of <see cref="Get"/>, which deliberately falls back to the key itself.
    /// </summary>
    public bool TryGet(string key, out string text)
    {
        string? value = Locale.GetString(key, _currentLocale);
        if (value is null)
        {
            text = string.Empty;
            return false;
        }
        text = value;
        return true;
    }

    /// <summary>Indexer form, convenient for non-XAML callers</summary>
    public string this[string key] => Get(key);

    /// <summary>
    /// Gets the translation for <paramref name="key"/> and substitutes <c>{0}</c>, <c>{1}</c>, ...
    /// with the supplied arguments. Use this for every parameterized locale entry.
    /// </summary>
    public string Format(string key, params object?[] args)
        => string.Format(CultureInfo.CurrentCulture, Get(key), args);

    /// <summary>
    /// Normalizes locale input ("zh-CN", "zh_CN", "zh", "cn" → "zhCN"; "en-US", "en", "en_US" → "enUS").
    /// Unknown values are returned unchanged so additional locale files can be added later.
    /// </summary>
    public static string Normalize(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale)) return ZH_CN;

        string compact = locale.Replace("-", string.Empty).Replace("_", string.Empty).Trim();
        if (compact.Equals("zh", StringComparison.OrdinalIgnoreCase) ||
            compact.Equals("cn", StringComparison.OrdinalIgnoreCase) ||
            compact.Equals("zhcn", StringComparison.OrdinalIgnoreCase))
            return ZH_CN;
        if (compact.Equals("en", StringComparison.OrdinalIgnoreCase) ||
            compact.Equals("enus", StringComparison.OrdinalIgnoreCase))
            return EN_US;

        return locale.Trim();
    }

    /// <summary>
    /// Switches the current locale. Returns <see langword="true"/> when the locale actually changed.
    /// </summary>
    public bool SetLocale(string? locale)
    {
        string normalized = Normalize(locale);
        if (_currentLocale.Equals(normalized, StringComparison.OrdinalIgnoreCase)) return false;
        CurrentLocale = normalized;
        return true;
    }

    /// <inheritdoc />
    public override string ToString() => _currentLocale;

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
