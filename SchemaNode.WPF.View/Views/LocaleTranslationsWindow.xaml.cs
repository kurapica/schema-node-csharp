using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using SchemaNode.WPF.View.Localization;
using SchemaNode.WPF.View.NodeWrapper;

namespace SchemaNode.WPF.View.Views;

/// <summary>
/// Translation dialog for <c>system.localestring</c>: one editable row per language, add and
/// delete allowed, opened from the form's entry button. It shares the row editor's sizing rule
/// (about two thirds of the owner, centered, clamped to the work area, resizable) and scrolls
/// its content, so a locale string with many languages never stretches the host screen.
/// </summary>
public partial class LocaleTranslationsWindow : Window
{
    private readonly ObservableCollection<LocaleStringTranslationRow> _rows;
    /// <summary>Writes the confirmed rows back; returning false keeps the dialog open.</summary>
    private Func<bool>? _commit;

    public LocaleTranslationsWindow(IEnumerable<LocaleStringTranslationRow> rows,
                                    IEnumerable<string> availableLanguages,
                                    string title,
                                    bool isEditable)
    {
        InitializeComponent();

        Title = title;
        IsEditable = isEditable;
        AvailableLanguages = availableLanguages.ToArray();
        _rows = new ObservableCollection<LocaleStringTranslationRow>(rows);
        RowList.ItemsSource = _rows;
    }

    /// <summary>Whether rows can be edited; a read-only host still gets to view the translations.</summary>
    public bool IsEditable { get; }

    /// <summary>
    /// Languages the row drop-down offers. This is only a candidate list: no row is prefilled from
    /// it, so an untouched dialog never produces entries that would then be validated.
    /// </summary>
    public IReadOnlyList<string> AvailableLanguages { get; }

    /// <summary>
    /// Opens the dialog for <paramref name="wrapper"/> and, when confirmed, writes the rows back.
    /// Returns whether the translations were applied.
    /// </summary>
    public static bool Edit(LocaleStringWrapper wrapper, Window? owner)
    {
        string title = LocaleManager.Instance.Get("wpf.locale.translations");
        if (!string.IsNullOrWhiteSpace(wrapper.Key))
            title = $"{title} · {wrapper.Key}";

        LocaleTranslationsWindow window = new(wrapper.CreateTranslationRows(), wrapper.AvailableLanguages,
            title, wrapper.IsEditable);
        window._commit = () => wrapper.ApplyTranslationRows(window._rows);

        Window? effectiveOwner = TableRowEditorWindowLayout.ResolveOwner(owner);
        if (effectiveOwner is not null && effectiveOwner != window)
            window.Owner = effectiveOwner;
        TableRowEditorWindowLayout.Apply(window, effectiveOwner);

        // The rows are written by OnConfirm so a failed write can keep the dialog open.
        return window.ShowDialog() == true;
    }

    private void OnAdd(object sender, RoutedEventArgs e)
    {
        if (!IsEditable) return;
        _rows.Add(new LocaleStringTranslationRow(string.Empty, string.Empty, exists: false));
    }

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        if (!IsEditable) return;
        if (sender is FrameworkElement { Tag: LocaleStringTranslationRow row })
            _rows.Remove(row);
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        // Commit any in-place text edit before reading the rows (Enter on the default button does
        // not move focus away from the focused text box by itself).
        Keyboard.ClearFocus();

        if (IsEditable && _commit is not null && !_commit())
        {
            MessageBox.Show(
                this,
                LocaleManager.Instance.Get("wpf.locale.translations.failed"),
                LocaleManager.Instance.Get("wpf.locale.translations"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }
}
