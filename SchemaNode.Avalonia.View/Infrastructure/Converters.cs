using Avalonia.Data.Converters;

namespace SchemaNode.Avalonia.View;

/// <summary>Stateless converters referenced from view bindings.</summary>
public static class Converters
{
    /// <summary>Boolean negation; null stays null. Avalonia has no inverse of <c>IsVisible</c> or
    /// <c>IsReadOnly</c>, so the editors use this instead of duplicating properties.</summary>
    public static readonly IValueConverter Not =
        new FuncValueConverter<bool, bool?>(value => !value);

    /// <summary>Whether the localized text is non-empty (drives a label's visibility).</summary>
    public static readonly IValueConverter IsTextNotEmpty =
        new FuncValueConverter<string?, bool>(value => !string.IsNullOrWhiteSpace(value));
}
