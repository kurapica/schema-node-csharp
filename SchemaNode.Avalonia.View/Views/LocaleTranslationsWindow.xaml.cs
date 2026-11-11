using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using SchemaNode.Avalonia.View.Localization;
using SchemaNode.Avalonia.View.NodeWrapper;

namespace SchemaNode.Avalonia.View.Views;

/// <summary>
/// Translation dialog for <c>system.localestring</c>: one editable row per language, add and
/// delete allowed, opened from the form's entry button. It shares the row editor's sizing rule
/// (about two thirds of the owner, centered, resizable) and scrolls its content, so a locale
/// string with many languages never stretches the host screen.
/// </summary>
public partial class LocaleTranslationsWindow : Window
{
    private readonly ObservableCollection<LocaleStringTranslationRow> _rows;
    /// <summary>Writes the confirmed rows back; returning false keeps the dialog open.</summary>
    private Func<bool>? _commit;

    /// <summary>Parameterless constructor required by the XAML loader.</summary>
    public LocaleTranslationsWindow()
        : this([], [], string.Empty, isEditable: false)
    {
    }

    public LocaleTranslationsWindow(
        IEnumerable<LocaleStringTranslationRow> rows,
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
        AddButton.IsVisible = isEditable;
    }

    /// <summary>Whether rows can be edited; a read-only host still gets to view the translations.</summary>
    public bool IsEditable { get; }

    /// <summary>
    /// Languages the row drop-down offers. This is only a candidate list: no row is prefilled from
    /// it, so an untouched dialog never produces entries that would then be validated.
    /// </summary>
    public IReadOnlyList<string> AvailableLanguages { get; }

    /// <summary>The rows edited by the dialog, written back on confirm.</summary>
    public IReadOnlyList<LocaleStringTranslationRow> Rows => _rows;

    /// <summary>
    /// Opens the dialog for <paramref name="wrapper"/> and, when confirmed, writes the rows back.
    /// Returns whether the translations were applied.
    /// </summary>
    public static async Task<bool> EditAsync(LocaleStringWrapper wrapper, Window? owner)
    {
        string title = LocaleManager.Instance.Get("avalonia.locale.translations");
        if (!string.IsNullOrWhiteSpace(wrapper.Key))
            title = $"{title} · {wrapper.Key}";

        LocaleTranslationsWindow window = new(
            wrapper.CreateTranslationRows(), wrapper.AvailableLanguages, title, wrapper.IsEditable);
        window._commit = () => wrapper.ApplyTranslationRows(window._rows);

        Window? effectiveOwner = ResolveOwner(owner);
        TableRowEditorWindowLayout.Apply(window, effectiveOwner);

        if (effectiveOwner is null)
        {
            // No owning window (for example a headless host): show non-modally and report cancel.
            window.Show();
            return false;
        }

        // The rows are written by OnConfirm so a failed write can keep the dialog open.
        return await window.ShowDialog<bool>(effectiveOwner).ConfigureAwait(true);
    }

    private static Window? ResolveOwner(Window? preferred)
    {
        if (preferred is not null) return preferred;
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return null;

        Window? active = desktop.Windows.FirstOrDefault(w => w.IsVisible && w.IsActive);
        if (active is not null) return active;

        Window? visible = desktop.Windows.FirstOrDefault(w => w.IsVisible);
        return visible ?? desktop.MainWindow;
    }

    private void OnAdd(object? sender, RoutedEventArgs e)
    {
        if (!IsEditable) return;
        _rows.Add(new LocaleStringTranslationRow(string.Empty, string.Empty, exists: false));
    }

    private void OnDelete(object? sender, RoutedEventArgs e)
    {
        if (!IsEditable) return;
        if (sender is Button { Tag: LocaleStringTranslationRow row })
            _rows.Remove(row);
    }

    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        // Commit any in-place text edit before reading the rows (Enter on the default button does
        // not move focus away from the focused text box by itself).
        AddButton.Focus();

        if (IsEditable && _commit is not null && !_commit())
        {
            ErrorText.Text = LocaleManager.Instance.Get("avalonia.locale.translations.failed");
            ErrorText.IsVisible = true;
            return;
        }

        Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
