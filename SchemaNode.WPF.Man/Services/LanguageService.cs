using System;
using SchemaNode.WPF.View.Localization;

namespace SchemaNode.WPF.Man.Services;

/// <summary>
/// Coordinates the language switch:
///   1. update <see cref="LocaleManager"/> so XAML text bindings refresh;
///   2. push the locale to every live <see cref="SchemaContext"/> through <see cref="SchemaContextFactory.ApplyLocale"/>;
///   3. persist the setting through <see cref="LanguageSettingsStore"/>.
///
/// On startup <see cref="InitializeAsync"/> reads the saved locale first, so newly created screen
/// contexts are initialized with the persisted language instead of a UI default.
/// </summary>
public sealed class LanguageService
{
    private readonly LanguageSettingsStore _store;
    private readonly SchemaContextFactory _factory;

    public LanguageService(LanguageSettingsStore store, SchemaContextFactory factory)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>Reads the persisted locale and applies it to the UI and the factory.</summary>
    public async Task InitializeAsync()
    {
        string? saved = null;
        try
        {
            saved = await _store.LoadAsync();
        }
        catch
        {
            // Keep the default locale when the setting cannot be read.
        }

        string locale = LocaleManager.Normalize(saved ?? _factory.CurrentLocale);
        _factory.ApplyLocale(locale);
        LocaleManager.Instance.SetLocale(locale);
    }

    /// <summary>Switches the locale for the UI and all live contexts, then persists it.</summary>
    public async Task SwitchAsync(string? locale)
    {
        string target = LocaleManager.Normalize(locale);

        // Runtime first: every live context must already hold the new locale, so the UI refresh
        // triggered below re-reads Core messages (and entry displays) in the new language.
        _factory.ApplyLocale(target);

        // UI refresh: bindings on LocaleManager.Version are re-evaluated in place.
        LocaleManager.Instance.SetLocale(target);

        try
        {
            await _store.SaveAsync(target);
        }
        catch
        {
            // The switch already took effect; a storage failure must not break the UI.
        }
    }
}
