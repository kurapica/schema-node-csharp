using SchemaNode.Context;
using SchemaNode.Struct;

namespace SchemaNode.WPF.View.Localization;

/// <summary>
/// Resolves the text shown for a schema entry, node or field, matching the vue frontend:
/// <c>_L(display ?? value)</c>. The <see cref="LocaleString"/> display wins; when it is absent or
/// resolves to an untranslated key the raw value is translated as a locale key; when even that
/// misses, the raw value itself is shown - never an empty string.
/// </summary>
/// <remarks>
/// Display resolution loads the referenced node type (<c>{@path}</c>) and can fail for data that
/// no longer matches the schema. That failure is contained per item here, so one broken schema
/// cannot make the whole entry list (and with it the selector) unusable.
/// </remarks>
internal static class LocalizedDisplay
{
    /// <summary>Resolves the text for an entry with <paramref name="display"/> and <paramref name="value"/>.</summary>
    public static string Resolve(LocaleString? display, string? value, ISchemaContext? context)
    {
        string? text = TryDisplay(display, context);
        if (!string.IsNullOrWhiteSpace(text)) return text;

        string? raw = string.IsNullOrWhiteSpace(value) ? null : value;
        if (raw is not null && LocaleManager.Instance.TryGet(raw, out string translated))
            return translated;

        return raw ?? string.Empty;
    }

    /// <summary>Resolves the text for a node that may have no display of its own.</summary>
    public static string Resolve(ISchemaContext? context, LocaleString? display, params string?[] fallbacks)
    {
        string? text = TryDisplay(display, context);
        if (!string.IsNullOrWhiteSpace(text)) return text;

        foreach (string? fallback in fallbacks)
        {
            if (string.IsNullOrWhiteSpace(fallback)) continue;
            if (LocaleManager.Instance.TryGet(fallback, out string translated)) return translated;
            return fallback;
        }

        return string.Empty;
    }

    /// <summary>
    /// The display text, or null when it is empty or still contains an unresolved template
    /// placeholder (a display key whose locale entries are missing must not leak braces into the UI).
    /// </summary>
    private static string? TryDisplay(LocaleString? display, ISchemaContext? context)
    {
        if (display is null) return null;

        try
        {
            string text = display.ToString(context!);
            if (string.IsNullOrWhiteSpace(text)) return null;
            if (text.Contains('{') || text.Contains('}')) return null;

            // The display key itself is returned when no translation is loaded. Prefer the UI
            // dictionary (LocaleManager) so a context whose locale lags still resolves.
            if (string.Equals(text, display.Key, StringComparison.Ordinal)
                && LocaleManager.Instance.TryGet(text, out string translated))
                return translated;

            return text;
        }
        catch
        {
            return null;
        }
    }
}
