using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using SchemaNode.Avalonia.View.Localization;

namespace SchemaNode.Avalonia.Man.Infrastructure;

/// <summary>
/// Modal confirmation used by the list screens before a destructive host operation. WPF used
/// <c>MessageBox.Show(..., YesNo)</c>, which blocks; Avalonia dialogs are asynchronous, so the
/// call sites await this service instead.
/// </summary>
public sealed class DialogService
{
    /// <summary>Asks the user to confirm an operation; false when there is no shell window.</summary>
    public async Task<bool> ConfirmAsync(string message, string? caption = null)
    {
        Window? owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (owner is null) return false;

        ConfirmWindow dialog = new(message, caption ?? LocaleManager.Instance.Get("avalonia.type.confirm.caption"));
        return await dialog.ShowDialog<bool>(owner).ConfigureAwait(false);
    }
}

/// <summary>Minimal code-built confirmation window (no XAML, two buttons, returns bool).</summary>
public sealed class ConfirmWindow : Window
{
    public ConfirmWindow(string message, string caption)
    {
        Title = caption;
        Width = 420;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;

        TextBlock text = new()
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(16),
        };

        Button cancel = new() { Content = LocaleManager.Instance.Get("avalonia.button.cancel"), MinWidth = 80 };
        Button ok = new() { Content = LocaleManager.Instance.Get("avalonia.button.ok"), MinWidth = 80 };
        cancel.Click += (_, _) => Close(false);
        ok.Click += (_, _) => Close(true);

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(16, 0, 16, 16),
            Children = { cancel, ok },
        };

        DockPanel panel = new() { LastChildFill = true, Children = { buttons, text } };
        DockPanel.SetDock(buttons, Dock.Bottom);

        Content = panel;
    }
}
