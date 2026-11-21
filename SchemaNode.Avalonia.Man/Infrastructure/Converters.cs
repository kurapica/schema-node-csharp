using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SchemaNode.Avalonia.Man.Infrastructure;

/// <summary>Converters used by the Man views.</summary>
public static class Converters
{
    /// <summary>True when the bound text is non-empty; drives Avalonia's boolean <c>IsVisible</c>.</summary>
    public static readonly IValueConverter VisibleIfText =
        new FuncValueConverter<string?, bool>(value => !string.IsNullOrWhiteSpace(value));

    /// <summary>True when the bound collection/bool is null/false; drives placeholders.</summary>
    public static readonly IValueConverter VisibleIfEmpty =
        new FuncValueConverter<object?, bool>(value => value is null);

    /// <summary>Inverts a boolean; used where the default is "bar shown".</summary>
    public static readonly IValueConverter Negate =
        new FuncValueConverter<bool, bool>(value => !value);

    /// <summary>Chat bubble background: user messages are green, assistant messages are blue.</summary>
    public static readonly IValueConverter UserBubbleBackground =
        new FuncValueConverter<bool, IBrush>(isUser => isUser
            ? new SolidColorBrush(Color.Parse("#E8F5E9"))
            : new SolidColorBrush(Color.Parse("#EEF3FB")));

    /// <summary>Error text color; normal answers keep the default foreground.</summary>
    public static readonly IValueConverter ErrorForeground =
        new FuncValueConverter<bool, IBrush?>(isError => isError
            ? new SolidColorBrush(Color.Parse("#B71C1C"))
            : null);
}

