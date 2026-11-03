using System;
using SchemaNode.Attribute;
using SchemaNode.Data;
using Application = SchemaNode.Property.App.App;
using EnableStorage = SchemaNode.Property.App.EnableStorage;
using SchemaType = SchemaNode.Property.Core.SchemaType;

namespace SchemaNode.WPF.Man.Services;

/// <summary>
/// The single struct persisted by the "system.wpf" system application.
/// It is registered automatically because the type carries [Meta&lt;App&gt;] and [Meta&lt;SchemaType&gt;].
/// </summary>
[Meta<Application>("system.wpf")]
[Meta<SchemaType>("system.wpf.setting")]
[Meta<EnableStorage>(true)]
public sealed class Setting
{
    /// <summary>Current UI locale code, e.g. "zhCN" or "enUS"</summary>
    public string? Locale { get; set; }
}

/// <summary>
/// Stores the WPF console setting through the same app-data mechanism used by the App layer.
///
/// The setting lives in the system application "system.wpf" under the single-struct field "setting"
/// (schema "system.wpf.setting"). Reads and writes use the app-data API, not a private file,
/// so more settings can be added by extending the <see cref="Setting"/> struct.
/// </summary>
public sealed class LanguageSettingsStore
{
    // System-level single-struct data still needs a target slot for the scope key.
    private static readonly string Target = Guid.Empty.ToString("D");

    private readonly SchemaContextFactory _factory;

    public LanguageSettingsStore(SchemaContextFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>
    /// Reads the saved locale; returns null when nothing has been saved yet.
    /// Uses the strongly typed entity API (<see cref="Setting"/> maps the single-struct field);
    /// it never loads the "system.wpf" app type at runtime.
    /// </summary>
    public async Task<string?> LoadAsync()
    {
        using SchemaScreenSession session = _factory.CreateSession();
        Setting? setting = await session.Context.GetEntityAsync<Setting>(Target);
        return setting?.Locale;
    }

    /// <summary>Writes the locale into the setting struct through the same entity API.</summary>
    public async Task SaveAsync(string locale)
    {
        using SchemaScreenSession session = _factory.CreateSession();
        await session.Context.SaveEntityAsync(Target, new Setting { Locale = locale });
    }
}
