using Avalonia.Controls;
using Avalonia.Interactivity;
using SchemaNode.Avalonia.View.Localization;
using SchemaNode.Avalonia.View.NodeWrapper;

namespace SchemaNode.Avalonia.View.Views;

/// <summary>
/// Built-in row editor: a modal window hosting the normal view machinery with role
/// <see cref="SchemaViewRole.RowEditor"/>, sized to roughly two thirds of the owner.
///
/// The edited node is detached from the array (a new row or a copy of an existing row), so the
/// dialog can validate it and the caller either writes the copy back or disposes it.
/// </summary>
public partial class TableRowEditorWindow : Window
{
    private DataNodeWrapper? _wrapper;
    private bool _confirming;

    public TableRowEditorWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Shows <paramref name="request"/> modally and returns whether the user confirmed. The
    /// callback is asynchronous because Avalonia dialogs complete through the dispatcher.
    /// </summary>
    public static async Task<bool> ShowAsync(TableRowEditorRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        TableRowEditorWindow window = new();
        window.Apply(request);

        Window? owner = request.EffectiveOwner;
        if (owner is null)
        {
            // No owning window (for example a headless host): show non-modally and report cancel.
            window.Show();
            return false;
        }

        return await window.ShowDialog<bool>(owner).ConfigureAwait(true);
    }

    private void Apply(TableRowEditorRequest request)
    {
        Title = request.Title;
        _wrapper = NodeWrapperFactory.Create(
            request.Node, request.Registry, null, SchemaViewRole.RowEditor);

        Editor.Registry = request.Registry;
        Editor.Role = SchemaViewRole.RowEditor;
        Editor.Skin = request.Skin;
        Editor.Node = _wrapper;

        request.ApplyLayoutTo(this);
        Closed += OnClosed;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _wrapper?.Dispose();
        _wrapper = null;
    }

    private async void OnConfirm(object? sender, RoutedEventArgs e)
    {
        if (_confirming) return;
        _confirming = true;
        try
        {
            if (_wrapper is null) return;

            // Validation is asynchronous in Core; force the whole subtree through it so a value
            // typed just before clicking OK cannot slip through on a pending result.
            if (!await RowEditorValidation.ValidateTreeAsync(_wrapper.Node))
            {
                ValidationError.Text = LocaleManager.Instance.Get("avalonia.table.error.invalid");
                ValidationError.IsVisible = true;
                return;
            }

            Close(true);
        }
        finally
        {
            _confirming = false;
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
