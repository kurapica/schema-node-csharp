using Avalonia.Controls;
using Avalonia.Interactivity;
using SchemaNode.Avalonia.View.NodeWrapper;

namespace SchemaNode.Avalonia.View.Views;

/// <summary>
/// Locale string editor: the key stays on the form, the translation table opens in a dialog so a
/// localized label does not expand into one field per language on the configuration screen.
/// </summary>
public partial class LocaleStringView : UserControl
{
    public LocaleStringView()
    {
        InitializeComponent();
    }

    private async void OnShowTranslations(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not LocaleStringWrapper wrapper) return;
        await LocaleTranslationsWindow.EditAsync(wrapper, TopLevel.GetTopLevel(this) as Window);
    }
}
