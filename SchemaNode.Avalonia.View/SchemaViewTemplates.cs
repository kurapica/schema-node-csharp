using Avalonia.Controls;
using SchemaNode.Avalonia.View.NodeWrapper;
using SchemaNode.Avalonia.View.Views;

namespace SchemaNode.Avalonia.View;

/// <summary>
/// Maps a template key to the control that renders a wrapper. This is the Avalonia counterpart of
/// the WPF template dictionary: the registry decides the key, this factory materializes the view.
///
/// Hosts override any key through <see cref="Set"/> (for example a compact struct view on one
/// screen) without touching the built-in registrations or the registry.
/// </summary>
public static class SchemaViewTemplates
{
    private static readonly Dictionary<string, Func<DataNodeWrapper, Control>> Factories =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [SchemaTemplateKeys.Input] = wrapper => new InputView { DataContext = wrapper },
            [SchemaTemplateKeys.Boolean] = wrapper => new BoolView { DataContext = wrapper },
            [SchemaTemplateKeys.Date] = wrapper => new DateView { DataContext = wrapper },
            [SchemaTemplateKeys.Year] = wrapper => new YearView { DataContext = wrapper },
            [SchemaTemplateKeys.Language] = wrapper => new LanguageView { DataContext = wrapper },
            [SchemaTemplateKeys.RangeDate] = wrapper => new RangeDateView { DataContext = wrapper },
            [SchemaTemplateKeys.LocaleString] = wrapper => new LocaleStringView { DataContext = wrapper },
            [SchemaTemplateKeys.FlagsEnum] = wrapper => new FlagsEnumView { DataContext = wrapper },
            [SchemaTemplateKeys.Struct] = wrapper => new StructView { DataContext = wrapper },
            [SchemaTemplateKeys.Array] = wrapper => new ArrayView { DataContext = wrapper },
            [SchemaTemplateKeys.Table] = wrapper => new Views.TableView { DataContext = wrapper },
            [SchemaTemplateKeys.TypeSelector] = wrapper => new TypeSelectorView { DataContext = wrapper },
            [SchemaTemplateKeys.Fallback] = wrapper => new FallbackView { DataContext = wrapper },
        };

    private static readonly Dictionary<string, Func<DataNodeWrapper, Control>> Overrides =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates the view for <paramref name="templateKey"/>.</summary>
    public static Control Create(string templateKey, DataNodeWrapper wrapper)
    {
        ArgumentNullException.ThrowIfNull(wrapper);

        if (Overrides.TryGetValue(templateKey, out Func<DataNodeWrapper, Control>? factory))
            return factory(wrapper);

        if (Factories.TryGetValue(templateKey, out factory))
            return factory(wrapper);

        // An unknown key (a host registered a template name it never provided) must render
        // something instead of throwing in the middle of layout.
        System.Diagnostics.Debug.WriteLine($"No view registered for template '{templateKey}'.");
        return new FallbackView { DataContext = wrapper };
    }

    /// <summary>Registers or replaces a template key. Passing null restores the built-in view.</summary>
    public static void Set(string templateKey, Func<DataNodeWrapper, Control>? factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateKey);

        if (factory is null)
            Overrides.Remove(templateKey);
        else
            Overrides[templateKey] = factory;
    }

    /// <summary>Whether a host override is installed for <paramref name="templateKey"/>.</summary>
    public static bool HasOverride(string templateKey) => Overrides.ContainsKey(templateKey);
}
