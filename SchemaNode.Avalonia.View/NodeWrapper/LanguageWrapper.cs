using SchemaNode.Avalonia.View.Localization;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Utility;

namespace SchemaNode.Avalonia.View.NodeWrapper;

/// <summary>
/// Editor for <c>system.language</c>.
///
/// A language is one of the configured locale codes, so free text is the wrong editor: it allows
/// typos and silently creates languages nothing can translate with. The wrapper exposes the
/// configured locales as a closed drop-down while leaving the node empty until the user picks a
/// value, so it never injects an implicit default or an extra required value.
/// </summary>
public sealed class LanguageWrapper : DataNodeWrapper
{
    /// <inheritdoc />
    public override string? DefaultTemplateKey => SchemaTemplateKeys.Language;

    public LanguageWrapper(DataNode node, SchemaViewRegistry? registry, string? fieldName, SchemaViewRole role)
        : base(node, registry, fieldName, role)
    {
        Languages = ReadLanguages(node);
    }

    /// <summary>Configured locale codes offered by the drop-down, sorted for stable display.</summary>
    public IReadOnlyList<string> Languages { get; }

    /// <summary>
    /// Two-way selected language. Null means "not chosen yet"; selecting or clearing writes through
    /// Core so the normal scalar constraints still apply.
    /// </summary>
    public string? SelectedLanguage
    {
        get
        {
            string? value = NodeImpl.GetValue<string>();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        set
        {
            if (IsDisposed || !IsEditable) return;
            if (string.IsNullOrWhiteSpace(value))
                NodeImpl.ClearValue();
            else
                NodeImpl.TrySetValue(value.Trim());
        }
    }

    /// <inheritdoc />
    protected override void OnValueChanged() => RaisePropertyChangedNow(nameof(SelectedLanguage));

    private static IReadOnlyList<string> ReadLanguages(DataNode node)
    {
        HashSet<string> languages = new(StringComparer.OrdinalIgnoreCase);

        IEnumerable<string> available = node.Context is SchemaContext context
            ? context.GetAvailableLocales()
            : [];
        foreach (string language in available)
        {
            if (!string.IsNullOrWhiteSpace(language))
                languages.Add(language);
        }

        if (!string.IsNullOrWhiteSpace(LocaleManager.Instance.CurrentLocale))
            languages.Add(LocaleManager.Instance.CurrentLocale);

        // Keep a value that only exists in this node so opening the editor never hides it.
        string? current = node.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(current))
            languages.Add(current.Trim());

        return languages.OrderBy(l => l, StringComparer.Ordinal).ToArray();
    }
}
