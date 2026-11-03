using System.Windows.Input;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using Prism.Navigation.Regions;
using SchemaNode.Schema;
using SchemaNode.WPF.Man.Services;
using SchemaNode.WPF.Man.Views;
using SchemaNode.WPF.View.Localization;

namespace SchemaNode.WPF.Man.ViewModels;

/// <summary>
/// Type definition document screen (查看 / 编辑 / 新建).
///
/// In the vue reference these flows open an <c>el-drawer</c> over the list. In WPF the same flows
/// are region routes, so the list screen is replaced by this screen and a "返回列表" button
/// navigates back with the current namespace preserved.
///
/// The screen owns its own <see cref="SchemaScreenSession"/>: a definition node must never outlive
/// the screen that created it, and the session's Reverse dispose order is Wrapper → DataNode → Context.
/// </summary>
public class TypeDocumentViewModel : BindableBase, INavigationAware, IDestructible
{
    public const string ModeParameter = "mode";
    public const string NameParameter = "name";
    public const string NamespaceParameter = "namespace";

    public const string ModeView = "view";
    public const string ModeEdit = "edit";
    public const string ModeNew = "new";

    private const string MainContentRegion = "MainContentRegion";

    private readonly SchemaContextFactory _factory;
    private readonly IRegionManager _regionManager;

    private SchemaScreenSession? _session;
    private string _mode = ModeView;
    private string _fullName = string.Empty;
    private string _namespace = string.Empty;
    private string? _status;
    private bool _isLoading;
    private bool _isSaving;
    private bool _isNavigating;

    public TypeDocumentViewModel(SchemaContextFactory factory, IRegionManager regionManager, SchemaEditorViewModel editor)
    {
        _factory = factory;
        _regionManager = regionManager;
        Editor = editor;

        SaveCommand = new DelegateCommand(ExecuteSave, () => CanSave)
            .ObservesProperty(() => CanSave);
        BackCommand = new DelegateCommand(ExecuteBack);
    }

    /// <summary>Generic definition editor hosted by this screen.</summary>
    public SchemaEditorViewModel Editor { get; }

    /// <summary>Current document mode (view / edit / new).</summary>
    public string Mode => _mode;

    /// <summary>Whether the property area accepts input (false for 查看).</summary>
    public bool IsEditable => _mode is ModeEdit or ModeNew;

    /// <summary>Whether the save button is enabled.</summary>
    public bool CanSave => IsEditable && !_isSaving;

    /// <summary>Header text describing the mode and subject.</summary>
    public string HeaderText => _mode switch
    {
        ModeNew => LocaleManager.Instance.Format("wpf.typedoc.header.new", _namespace),
        ModeEdit => LocaleManager.Instance.Format("wpf.typedoc.header.edit", _fullName),
        _ => LocaleManager.Instance.Format("wpf.typedoc.header.view", _fullName),
    };

    /// <summary>Loading / operation result message.</summary>
    public string? Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public ICommand SaveCommand { get; }
    public ICommand BackCommand { get; }

    #region Navigation

    public bool IsNavigationTarget(NavigationContext navigationContext) => false;

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        Teardown();
        try
        {
            _session = _factory.CreateSession();
            Editor.Attach(_session);

            _mode = navigationContext.Parameters.GetValue<string>(ModeParameter) ?? ModeView;
            _fullName = navigationContext.Parameters.GetValue<string>(NameParameter) ?? string.Empty;
            _namespace = navigationContext.Parameters.GetValue<string>(NamespaceParameter) ?? string.Empty;

            RaisePropertyChanged(nameof(Mode));
            RaisePropertyChanged(nameof(IsEditable));
            RaisePropertyChanged(nameof(CanSave));
            RaisePropertyChanged(nameof(HeaderText));

            _ = LoadAsync();
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    public void OnNavigatedFrom(NavigationContext navigationContext) => Teardown();

    public void Destroy() => Teardown();

    #endregion

    private async Task LoadAsync()
    {
        IsLoading = true;
        Status = _mode == ModeNew
            ? LocaleManager.Instance.Format("wpf.typedoc.status.loading.new", _namespace)
            : LocaleManager.Instance.Format("wpf.typedoc.status.loading", _fullName);

        try
        {
            if (_mode == ModeNew)
            {
                await Editor.NewSchemaAsync(new NodeSchema { Namespace = _namespace });
                Status = LocaleManager.Instance.Get("wpf.typedoc.status.newready");
                return;
            }

            await Editor.ReloadSchemaAsync(_fullName);
            Status = Editor.Node is null && Editor.Error is not null
                ? LocaleManager.Instance.Format("wpf.typedoc.status.error", Editor.Error)
                : null;
        }
        catch (Exception ex)
        {
            Status = LocaleManager.Instance.Format("wpf.typedoc.status.error", ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async void ExecuteSave()
    {
        SchemaScreenSession? session = _session;
        if (session is null || !CanSave) return;

        _isSaving = true;
        RaisePropertyChanged(nameof(CanSave));

        try
        {
            NodeSchema? schema = await Editor.GetNodeSchemaAsync();
            if (schema is null)
            {
                Status = LocaleManager.Instance.Get("wpf.typedoc.status.noschema");
                return;
            }

            if (string.IsNullOrWhiteSpace(schema.Namespace))
            {
                Status = LocaleManager.Instance.Get("wpf.typedoc.status.nonamespace");
                return;
            }

            if (string.IsNullOrWhiteSpace(schema.Name))
            {
                Status = LocaleManager.Instance.Get("wpf.typedoc.status.noname");
                return;
            }

            bool saved = await Task.Run(() => session.Api.SaveSchemaAsync(schema));
            if (!saved)
            {
                Status = LocaleManager.Instance.Format("wpf.typedoc.status.savefailed", schema.FullName);
                return;
            }

            // Saving marks the type Loaded=false; rebuild it inside this screen so the old node is
            // never kept alive by any subscription.
            _fullName = schema.FullName;
            _namespace = schema.Namespace ?? _namespace;
            _mode = ModeEdit;
            RaisePropertyChanged(nameof(Mode));
            RaisePropertyChanged(nameof(IsEditable));
            RaisePropertyChanged(nameof(HeaderText));

            await Editor.ReloadSchemaAsync(_fullName, reload: true);
            Status = LocaleManager.Instance.Format("wpf.typedoc.status.saved", _fullName);
        }
        catch (Exception ex)
        {
            Status = LocaleManager.Instance.Format("wpf.typedoc.status.savefailed", ex.Message);
        }
        finally
        {
            _isSaving = false;
            RaisePropertyChanged(nameof(CanSave));
        }
    }

    private async void ExecuteBack()
    {
        if (_isNavigating) return;
        _isNavigating = true;
        try
        {
            NavigationParameters parameters = new()
            {
                { NamespaceParameter, _namespace.Length > 0 ? _namespace : "system" },
            };

            TaskCompletionSource<INavigationResult> completion = new();
            _regionManager.RequestNavigate(
                MainContentRegion,
                nameof(TypeListView),
                result => completion.TrySetResult(result),
                parameters);

            INavigationResult outcome = await completion.Task;
            if (!outcome.Success)
            {
                Status = LocaleManager.Instance.Format(
                    "wpf.typedoc.status.navfailed",
                    outcome.Exception?.Message ?? nameof(TypeListView));
            }
        }
        finally
        {
            _isNavigating = false;
        }
    }

    private void Teardown()
    {
        Editor.Dispose();
        _session?.Dispose();
        _session = null;
        Status = null;
        IsLoading = false;
    }
}
