using System.Windows;
using System.Windows.Controls;
using SchemaNode.WPF.View.NodeWrapper;

namespace SchemaNode.WPF.View.Views;

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

    private void OnShowTranslations(object sender, RoutedEventArgs e)
    {
        if (DataContext is not LocaleStringWrapper wrapper) return;
        LocaleTranslationsWindow.Edit(wrapper, Window.GetWindow(this));
    }
}
