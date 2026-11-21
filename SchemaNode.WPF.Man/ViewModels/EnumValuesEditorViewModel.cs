using System.Collections.ObjectModel;
using System.Windows.Input;
using Prism.Commands;
using Prism.Mvvm;
using SchemaNode.Context;
using SchemaNode.Property.Common;
using SchemaNode.Runtime;
using SchemaNode.Schema.Provider;
using SchemaNode.Struct;
using SchemaNode.WPF.View.Localization;

namespace SchemaNode.WPF.Man.ViewModels;

/// <summary>
/// Multi-level cascading enum child-entry editor.
///
/// Parent entries are maintained by the <c>values</c> array (through the generic Attach property area); this editor only handles
/// "select a parent entry → edit its child entries", and writes go through the App-side
/// <see cref="SchemaStorageProviderExtension.SaveEnumEntriesAsync"/>.
/// It uses the screen session's SchemaContext and never creates its own Context / nodes.
/// </summary>
public class EnumValuesEditorViewModel : BindableBase, IDisposable
{
    private SchemaContext? _context;
    private string? _enumName;
    private string? _selectedParent;
    private EnumEntryRow? _selectedChild;
    private string? _status;

    public EnumValuesEditorViewModel()
    {
        LoadCommand = new DelegateCommand(ExecuteLoad, () => CanEdit)
            .ObservesProperty(() => SelectedParent);
        AddCommand = new DelegateCommand(AddChild, () => CanEdit)
            .ObservesProperty(() => SelectedParent);
        RemoveCommand = new DelegateCommand(RemoveChild, () => SelectedChild != null)
            .ObservesProperty(() => SelectedChild);
        SaveCommand = new DelegateCommand(ExecuteSave, () => CanEdit)
            .ObservesProperty(() => SelectedParent);
    }

    /// <summary>Available parent entry values</summary>
    public ObservableCollection<string> ParentValues { get; } = new();

    /// <summary>Child entries under the current parent entry</summary>
    public ObservableCollection<EnumEntryRow> Children { get; } = new();

    /// <summary>Currently selected parent entry</summary>
    public string? SelectedParent
    {
        get => _selectedParent;
        set => SetProperty(ref _selectedParent, value);
    }

    /// <summary>Currently selected child entry</summary>
    public EnumEntryRow? SelectedChild
    {
        get => _selectedChild;
        set => SetProperty(ref _selectedChild, value);
    }

    /// <summary>Operation status message</summary>
    public string? Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public ICommand LoadCommand { get; }
    public ICommand AddCommand { get; }
    public ICommand RemoveCommand { get; }
    public ICommand SaveCommand { get; }

    private bool CanEdit => _context != null && !string.IsNullOrWhiteSpace(_enumName)
                            && !string.IsNullOrWhiteSpace(_selectedParent);

    /// <summary>
    /// Binds to the screen session's Context and the enum type's full name, and immediately loads parent entries.
    /// </summary>
    public void Attach(SchemaContext context, string enumName)
    {
        Detach();
        _context = context;
        _enumName = enumName;
        _ = LoadParentsAsync();
    }

    /// <summary>Unbinds and clears the UI collections (does not dispose the Context; the Context belongs to the screen).</summary>
    public void Detach()
    {
        _context = null;
        _enumName = null;
        _selectedParent = null;
        _selectedChild = null;
        ParentValues.Clear();
        Children.Clear();
        Status = null;
    }

    /// <inheritdoc />
    public void Dispose() => Detach();

    private async void ExecuteLoad() => await LoadChildrenAsync();

    private void AddChild()
    {
        Children.Add(new EnumEntryRow());
        SelectedChild = Children[^1];
    }

    private void RemoveChild()
    {
        if (SelectedChild is null) return;
        Children.Remove(SelectedChild);
        SelectedChild = Children.LastOrDefault();
    }

    private async void ExecuteSave()
    {
        if (_context is null || _enumName is null || _selectedParent is null) return;

        List<Entry<string>> entries = [];
        foreach (EnumEntryRow row in Children)
        {
            string value = row.Value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(value)) continue;

            Entry<string> entry = new() { Value = value, Children = null };
            string display = row.Display?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(display))
            {
                Display displayProperty = new();
                displayProperty.SetValue(new LocaleString { Key = display });
                entry.SetProperty(displayProperty);
            }
            entries.Add(entry);
        }

        if (entries.Count == 0)
        {
            Status = LocaleManager.Instance.Get("wpf.enum.status.nothing");
            return;
        }

        bool ok = await _context.SaveEnumEntriesAsync(_enumName, _selectedParent, entries.ToArray());
        Status = ok
            ? LocaleManager.Instance.Format("wpf.enum.status.saved", _selectedParent, entries.Count)
            : LocaleManager.Instance.Get("wpf.enum.status.savefailed");

        if (ok) await LoadParentsAsync();
    }

    /// <summary>
    /// Parent entries come from the root-level children of EnumType. The parents themselves are maintained by the generic values array.
    /// </summary>
    private async Task LoadParentsAsync()
    {
        if (_context is null || string.IsNullOrWhiteSpace(_enumName)) return;

        try
        {
            EnumType? enumType = await _context.GetNodeTypeAsync<EnumType>(_enumName, reload: true);
            if (enumType is null)
            {
                Status = LocaleManager.Instance.Format("wpf.enum.status.notfound", _enumName);
                return;
            }

            EntryAccess<string>[] access = await enumType.GetEnumEntryAccessAsync(_context, null);
            Entry<string>[] parents = access.Length > 0 ? access[0].Children ?? [] : [];

            string? previous = _selectedParent;
            ParentValues.Clear();
            foreach (Entry<string> parent in parents)
                ParentValues.Add(parent.Value ?? string.Empty);

            SelectedParent = previous != null && ParentValues.Contains(previous)
                ? previous
                : ParentValues.FirstOrDefault();

            if (_selectedParent is null)
            {
                Children.Clear();
                Status = LocaleManager.Instance.Get("wpf.enum.status.noparent");
                return;
            }

            await LoadChildrenAsync();
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    private async Task LoadChildrenAsync()
    {
        if (_context is null || string.IsNullOrWhiteSpace(_enumName) || string.IsNullOrWhiteSpace(_selectedParent))
        {
            Children.Clear();
            return;
        }

        try
        {
            EnumType? enumType = await _context.GetNodeTypeAsync<EnumType>(_enumName);
            if (enumType is null) return;

            EntryAccess<string>[] access = await enumType.GetEnumEntryAccessAsync(_context, _selectedParent);
            Entry<string>[] children = access.Length > 0 ? access[^1].Children ?? [] : [];

            Children.Clear();
            foreach (Entry<string> child in children)
            {
                string display = child.GetProperty<Display>()?.Value?.ToString(_context) ?? child.Value ?? string.Empty;
                Children.Add(new EnumEntryRow { Value = child.Value ?? string.Empty, Display = display });
            }

            Status = LocaleManager.Instance.Format("wpf.enum.status.children", _selectedParent, Children.Count);
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }
}

/// <summary>
/// Editable row for a cascade child entry.
/// </summary>
public class EnumEntryRow : BindableBase
{
    private string _value = string.Empty;
    private string _display = string.Empty;

    /// <summary>Entry value written to the enum (required)</summary>
    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    /// <summary>Display text (optional)</summary>
    public string Display
    {
        get => _display;
        set => SetProperty(ref _display, value);
    }
}
