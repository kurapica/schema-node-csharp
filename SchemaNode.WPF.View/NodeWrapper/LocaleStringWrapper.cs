using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Struct;
using SchemaNode.Utility;
using SchemaNode.WPF.View.Localization;

namespace SchemaNode.WPF.View.NodeWrapper;

/// <summary>
/// Editor for <c>system.localestring</c> (counterpart of localeStringView.vue).
///
/// The node is a struct (<c>key</c> + <c>trans</c>), but the form must not expand it: a localized
/// label can carry one translation per configured language, so inlining the array would flood the
/// configuration screen. The form therefore keeps a single text field for the key plus an entry
/// button whose summary names the current-language translation (or the number of entries), and
/// every translation row is edited inside a dialog.
/// </summary>
public sealed class LocaleStringWrapper : DataNodeWrapper
{
    public LocaleStringWrapper(DataNode node, SchemaViewRegistry? registry, string? fieldName, SchemaViewRole role,
                               bool ancestorVisibility = true)
        : base(node, registry, fieldName, role, ancestorVisibility)
    {
        StructNodeImpl = node as StructNode
            ?? throw new ArgumentException($"Node '{node.Type.Name}' is not a struct node.", nameof(node));
    }

    private StructNode StructNodeImpl { get; }

    /// <inheritdoc />
    public override string? DefaultTemplateKey => SchemaTemplateKeys.LocaleString;

    /// <summary>The default key edited by the single form field.</summary>
    public string Key
    {
        get => ReadValue()?.Key ?? string.Empty;
        set
        {
            if (IsDisposed || !IsEditable) return;
            if (StructNodeImpl.GetAccessValue("key") is DataNode key
                && string.Equals(key.GetValue<string>(), value, StringComparison.Ordinal))
                return;

            StructNodeImpl.TrySetFieldValue("key", value ?? string.Empty);
            RaisePropertyChangedNow(nameof(Key));
            RaisePropertyChangedNow(nameof(Summary));
        }
    }

    /// <summary>
    /// Summary shown inside the entry button: the current-language translation when it exists,
    /// otherwise the number of filled translations, and finally an explicit placeholder so an
    /// untranslated key never renders as a blank button.
    /// </summary>
    public string Summary
    {
        get
        {
            LocaleString? value = ReadValue();
            LocaleTran? current = value?.Trans?.FirstOrDefault(t =>
                MatchesLocale(t.Lang, LocaleManager.Instance.CurrentLocale));

            string? text = current?.Tran;
            if (!string.IsNullOrWhiteSpace(text)) return text;

            int count = CountTranslations(value);
            if (count > 0)
                return LocaleManager.Instance.Format("wpf.locale.translations.count", count);

            return LocaleManager.Instance.Get("wpf.locale.translations.empty");
        }
    }

    /// <summary>Number of translations that actually carry text.</summary>
    public int TranslationCount => CountTranslations(ReadValue());

    /// <summary>
    /// Languages offered by the dialog's language drop-down. It is only a candidate list: no entry
    /// is preselected, so opening the dialog never creates an empty translation row that would then
    /// participate in validation.
    /// </summary>
    public IReadOnlyList<string> AvailableLanguages
    {
        get
        {
            HashSet<string> languages = new(StringComparer.OrdinalIgnoreCase);

            IEnumerable<string> available = NodeImpl.Context is SchemaContext context
                ? context.GetAvailableLocales()
                : [];
            foreach (string language in available)
            {
                if (!string.IsNullOrWhiteSpace(language))
                    languages.Add(language);
            }

            if (!string.IsNullOrWhiteSpace(LocaleManager.Instance.CurrentLocale))
                languages.Add(LocaleManager.Instance.CurrentLocale);

            foreach (LocaleTran tran in ReadValue()?.Trans ?? [])
            {
                if (!string.IsNullOrWhiteSpace(tran.Lang))
                    languages.Add(tran.Lang);
            }

            return languages.OrderBy(l => l, StringComparer.Ordinal).ToArray();
        }
    }

    /// <summary>
    /// Snapshots the rows the translation dialog edits. Only translations that already exist in the
    /// node produce a row; configured languages are candidates for a new row but are never prefilled,
    /// because an untouched row must not be treated as an existing translation entry.
    /// </summary>
    public IReadOnlyList<LocaleStringTranslationRow> CreateTranslationRows()
    {
        LocaleString? value = ReadValue();
        Dictionary<string, string> translate = new(StringComparer.OrdinalIgnoreCase);
        foreach (LocaleTran tran in value?.Trans ?? [])
        {
            if (!string.IsNullOrWhiteSpace(tran.Lang))
                translate[tran.Lang] = tran.Tran ?? string.Empty;
        }

        return translate
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new LocaleStringTranslationRow(pair.Key, pair.Value, exists: true))
            .ToArray();
    }

    /// <summary>Writes the dialog rows back to the <c>trans</c> array.</summary>
    /// <returns>
    /// False when the write was rejected (for example because the node is no longer editable), so
    /// the dialog can stay open instead of pretending the change was saved.
    /// </returns>
    public bool ApplyTranslationRows(IEnumerable<LocaleStringTranslationRow> rows)
    {
        if (IsDisposed) return false;
        if (!IsEditable) return true;

        List<LocaleTran> trans = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (LocaleStringTranslationRow row in rows)
        {
            string language = (row.Lang ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(language) || !seen.Add(language)) continue;
            if (!row.Exists && string.IsNullOrWhiteSpace(row.Tran)) continue;
            trans.Add(new LocaleTran(language, row.Tran));
        }

        return StructNodeImpl.TrySetFieldValue("trans", trans.ToArray());
    }

    /// <inheritdoc />
    protected override void OnValueChanged()
    {
        Post(() =>
        {
            RaisePropertyChangedNow(nameof(Key));
            RaisePropertyChangedNow(nameof(Summary));
            RaisePropertyChangedNow(nameof(TranslationCount));
        });
    }

    /// <inheritdoc />
    protected override void OnLanguageChanged()
    {
        Post(() => RaisePropertyChangedNow(nameof(Summary)));
    }

    private LocaleString? ReadValue() => StructNodeImpl.GetValue<LocaleString>();

    private static int CountTranslations(LocaleString? value)
        => value?.Trans?.Count(t => !string.IsNullOrWhiteSpace(t.Lang) && !string.IsNullOrWhiteSpace(t.Tran)) ?? 0;

    private static bool MatchesLocale(string? language, string locale)
        => !string.IsNullOrWhiteSpace(language)
           && (language.StartsWith(locale, StringComparison.OrdinalIgnoreCase)
               || locale.StartsWith(language, StringComparison.OrdinalIgnoreCase));
}

/// <summary>One editable row of the translation dialog.</summary>
public sealed class LocaleStringTranslationRow
{
    public LocaleStringTranslationRow(string lang, string? tran, bool exists)
    {
        Lang = lang;
        Tran = tran ?? string.Empty;
        Exists = exists;
    }

    /// <summary>
    /// Language code selected in the drop-down, or null before the user picks one. A row without a
    /// language is ignored on save.
    /// </summary>
    public string? Lang { get; set; }

    /// <summary>Translation text for this language.</summary>
    public string Tran { get; set; }

    /// <summary>Whether the node already had an entry for this language when the dialog opened.</summary>
    public bool Exists { get; }
}
