using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using SchemaNode.Avalonia.Man.Infrastructure;
using SchemaNode.Avalonia.Man.Navigation;
using SchemaNode.AI.Chat;
using SchemaNode.Schema;
using SchemaNode.Avalonia.Man.Services;
using SchemaNode.Avalonia.Man.Views;
using SchemaNode.Avalonia.View.Localization;
using SchemaNode.Utility;

namespace SchemaNode.Avalonia.Man.ViewModels;

/// <summary>
/// Type definition document screen (view / edit / new).
///
/// In the vue reference these flows open an <c>el-drawer</c> over the list. In WPF the same flows
/// are region routes, so the list screen is replaced by this screen and a "Back to list" button
/// navigates back with the current namespace preserved.
///
/// The screen owns its own <see cref="SchemaScreenSession"/>: a definition node must never outlive
/// the screen that created it, and the session's Reverse dispose order is Wrapper → DataNode → Context.
///
/// The assistant pane is screen state as well. It resolves <see cref="IAIChat"/> from the screen
/// session, never from a provider, and cancels every turn before the session is torn down.
/// </summary>
public class TypeDocumentViewModel : BindableBase, INavigationAware, IDestructible
{
    public const string ModeParameter = "mode";
    public const string NameParameter = "name";
    public const string NamespaceParameter = "namespace";

    public const string ModeView = "view";
    public const string ModeEdit = "edit";
    public const string ModeNew = "new";

    /// <summary>
    /// Opens the new-definition screen seeded with a copy of an existing definition: same fields,
    /// no name, no namespace children and no source identity.
    /// </summary>
    public const string ModeCopy = "copy";

    private const string MainContentRegion = "MainContentRegion";
    private const string ChatRoleUser = "user";
    private const string ChatRoleAssistant = "assistant";
    private const string AiChatFeature = "ai_chat";

    /// <summary>
    /// Relation-driven visibility on a definition node is written by a debounced process. The
    /// document screen waits this long between building the node and binding the root view, so
    /// the attach groups that are about to be hidden never get an element tree.
    /// </summary>
    private static readonly TimeSpan DefinitionSettleDelay = TimeSpan.FromMilliseconds(500);

    private readonly SchemaContextFactory _factory;
    private readonly IRegionManager _regionManager;
    private readonly ObservableCollection<TypeChatMessage> _chatMessages = [];

    private SchemaScreenSession? _session;
    private string _mode = ModeView;
    private string _fullName = string.Empty;
    private string _namespace = string.Empty;
    private string? _status;
    private bool _isLoading;
    private bool _isSaving;
    private bool _isNavigating;
    private string _chatInput = string.Empty;
    private bool _isChatBusy;
    private bool _isChatVisible;
    private string? _conversationId;
    private string? _validationFeedback;
    private CancellationTokenSource? _chatCancellation;

    public TypeDocumentViewModel(SchemaContextFactory factory, IRegionManager regionManager, SchemaEditorViewModel editor)
    {
        _factory = factory;
        _regionManager = regionManager;
        Editor = editor;
        Editor.NodePublishDelay = DefinitionSettleDelay;

        SaveCommand = new DelegateCommand(ExecuteSave, () => CanSave)
            .ObservesProperty(() => CanSave);
        BackCommand = new DelegateCommand(ExecuteBack);
        CopyCommand = new DelegateCommand(ExecuteCopy);

        SendChatCommand = new DelegateCommand(ExecuteSendChat, () => CanSendChat)
            .ObservesProperty(() => ChatInput)
            .ObservesProperty(() => IsChatBusy);
        StopChatCommand = new DelegateCommand(ExecuteStopChat, () => IsChatBusy)
            .ObservesProperty(() => IsChatBusy);
        ClearChatCommand = new DelegateCommand(ExecuteClearChat);
        ToggleChatCommand = new DelegateCommand(ExecuteToggleChat);
    }

    /// <summary>Generic definition editor hosted by this screen.</summary>
    public SchemaEditorViewModel Editor { get; }

    /// <summary>Current document mode (view / edit / new / copy).</summary>
    public string Mode => _mode;

    /// <summary>Whether the screen creates a new definition (blank seed or copied seed).</summary>
    private bool IsNewFlow => _mode is ModeNew or ModeCopy;

    /// <summary>Whether the property area accepts input (false for view).</summary>
    public bool IsEditable => IsNewFlow || _mode == ModeEdit;

    /// <summary>Whether the save button is enabled.</summary>
    public bool CanSave => IsEditable && !_isSaving;

    /// <summary>Whether the copy action is offered: only an existing definition can be copied.</summary>
    public bool CanCopy => (_mode is ModeView or ModeEdit) && _fullName.Length > 0;

    /// <summary>Header text describing the mode and subject.</summary>
    public string HeaderText => _mode switch
    {
        ModeNew or ModeCopy => LocaleManager.Instance.Format("avalonia.typedoc.header.new", _namespace),
        ModeEdit => LocaleManager.Instance.Format("avalonia.typedoc.header.edit", _fullName),
        _ => LocaleManager.Instance.Format("avalonia.typedoc.header.view", _fullName),
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

    /// <summary>Messages rendered in the assistant pane.</summary>
    public ObservableCollection<TypeChatMessage> ChatMessages => _chatMessages;

    /// <summary>The assistant input box content.</summary>
    public string ChatInput
    {
        get => _chatInput;
        set
        {
            if (SetProperty(ref _chatInput, value))
                SendChatCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>Whether an assistant turn is running.</summary>
    public bool IsChatBusy
    {
        get => _isChatBusy;
        private set
        {
            if (!SetProperty(ref _isChatBusy, value)) return;
            RaisePropertyChanged(nameof(CanSendChat));
            SendChatCommand.RaiseCanExecuteChanged();
            StopChatCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>Whether a turn can be sent: editable screen, non-empty input and a registered assistant.</summary>
    public bool CanSendChat => IsEditable && !IsChatBusy
                               && !string.IsNullOrWhiteSpace(_chatInput)
                               && _session?.Chat is not null;

    /// <summary>Context line shown above the transcript.</summary>
    public string ChatContextText => IsNewFlow
        ? LocaleManager.Instance.Format("avalonia.typedoc.chat.context.new", _namespace)
        : LocaleManager.Instance.Format("avalonia.typedoc.chat.context", _fullName);

    /// <summary>
    /// Whether the backend declared the <c>ai_chat</c> feature and this screen actually has an
    /// assistant implementation. Same feature check as the frontend, answered in process.
    /// </summary>
    public bool HasAiChat => _session is { Chat: not null } && _session.Features.IsEnabled(AiChatFeature);

    /// <summary>Whether the assistant entry is offered: feature enabled and definition editable.</summary>
    public bool CanShowAiChat => HasAiChat && IsEditable;

    /// <summary>Whether the split assistant pane is currently open.</summary>
    public bool IsChatVisible
    {
        get => _isChatVisible;
        set => SetProperty(ref _isChatVisible, value);
    }

    public DelegateCommand SaveCommand { get; }
    public DelegateCommand BackCommand { get; }
    public DelegateCommand CopyCommand { get; }
    public DelegateCommand SendChatCommand { get; }
    public DelegateCommand StopChatCommand { get; }
    public DelegateCommand ClearChatCommand { get; }
    public DelegateCommand ToggleChatCommand { get; }

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

            _conversationId = null;
            _validationFeedback = null;
            _isChatVisible = false;

            RaisePropertyChanged(nameof(Mode));
            RaisePropertyChanged(nameof(IsEditable));
            RaisePropertyChanged(nameof(CanSave));
            RaisePropertyChanged(nameof(CanCopy));
            RaisePropertyChanged(nameof(HeaderText));
            RaisePropertyChanged(nameof(CanSendChat));
            RaisePropertyChanged(nameof(ChatContextText));
            RaisePropertyChanged(nameof(HasAiChat));
            RaisePropertyChanged(nameof(CanShowAiChat));
            RaisePropertyChanged(nameof(IsChatVisible));
            SendChatCommand.RaiseCanExecuteChanged();

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
        Status = IsNewFlow
            ? LocaleManager.Instance.Format("avalonia.typedoc.status.loading.new", _namespace)
            : LocaleManager.Instance.Format("avalonia.typedoc.status.loading", _fullName);

        try
        {
            if (_mode == ModeCopy)
            {
                // Copy opens the new screen, not the edit screen. The seed is the stored definition
                // with its name removed, so the target is a new type in the same namespace.
                NodeSchema? source = await Editor.LoadSavedDefinitionAsync(_fullName);
                if (source is null)
                {
                    Status = LocaleManager.Instance.Format("avalonia.typedoc.status.notfound", _fullName);
                    return;
                }

                NodeSchema seed = await Task.Run(() => SchemaDefinitionCopy.Create(source));
                await Editor.NewSchemaAsync(seed);
                if (Editor.GetNodeSchema()?.Namespace is { } copiedNamespace)
                    _namespace = copiedNamespace;
                Status = LocaleManager.Instance.Get("avalonia.typedoc.status.copyready");
                RaisePropertyChanged(nameof(ChatContextText));
                return;
            }

            if (_mode == ModeNew)
            {
                await Editor.NewSchemaAsync(new NodeSchema { Namespace = _namespace });
                Status = LocaleManager.Instance.Get("avalonia.typedoc.status.newready");
                RaisePropertyChanged(nameof(ChatContextText));
                return;
            }

            await Editor.ReloadSchemaAsync(_fullName);
            if (Editor.GetNodeSchema()?.Namespace is { } loadedNamespace)
                _namespace = loadedNamespace;
            Status = Editor.Node is null && Editor.Error is not null
                ? LocaleManager.Instance.Format("avalonia.typedoc.status.error", Editor.Error)
                : null;
            RaisePropertyChanged(nameof(ChatContextText));
        }
        catch (Exception ex)
        {
            Status = LocaleManager.Instance.Format("avalonia.typedoc.status.error", ex.Message);
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
                Status = LocaleManager.Instance.Get("avalonia.typedoc.status.noschema");
                return;
            }

            // The screen's namespace is supplied by navigation and must never be overwritten,
            // including by an AI write-back. An absent namespace may stay absent, but a supplied
            // namespace must be preserved exactly.
            if (!string.Equals(schema.Namespace ?? string.Empty, _namespace, StringComparison.Ordinal))
            {
                Status = LocaleManager.Instance.Format("avalonia.typedoc.status.namespacechanged", _namespace);
                return;
            }

            if (string.IsNullOrWhiteSpace(schema.Name))
            {
                Status = LocaleManager.Instance.Get("avalonia.typedoc.status.noname");
                return;
            }

            bool saved = await Task.Run(() => session.Api.SaveSchemaAsync(schema));
            if (!saved)
            {
                Status = LocaleManager.Instance.Format("avalonia.typedoc.status.savefailed", schema.FullName);
                return;
            }

            // Success: refresh the owning namespace and leave for the type list. The definition was
            // pulled from the parent namespace, so reloading it invalidates the saved child and pulls
            // the latest definition (including auths) into the runtime.
            await RefreshOwnerNamespaceAsync(session, schema);
            await NavigateToListAsync(schema.Namespace);
        }
        catch (Exception ex)
        {
            Status = LocaleManager.Instance.Format("avalonia.typedoc.status.savefailed", ex.Message);
        }
        finally
        {
            _isSaving = false;
            RaisePropertyChanged(nameof(CanSave));
        }
    }

    /// <summary>
    /// Reloads the namespace that owns the saved definition. A failure only reports the refresh
    /// error; the save itself already succeeded and navigation still happens.
    /// </summary>
    private async Task RefreshOwnerNamespaceAsync(SchemaScreenSession session, NodeSchema schema)
    {
        string? owner = schema.Namespace;
        if (string.IsNullOrWhiteSpace(owner)) return;

        try
        {
            await session.Context.GetNodeTypeAsync(owner, reload: true);
        }
        catch (Exception ex)
        {
            Status = LocaleManager.Instance.Format("avalonia.typedoc.status.error", ex.Message);
        }
    }

    private async void ExecuteBack() => await NavigateToListAsync(_namespace);

    /// <summary>
    /// Opens the same document route in copy mode. The route loads the stored definition and feeds
    /// a name-less clone into the new-definition flow.
    /// </summary>
    private async void ExecuteCopy()
    {
        if (_isNavigating || !CanCopy) return;
        _isNavigating = true;
        try
        {
            NavigationParameters parameters = new()
            {
                { ModeParameter, ModeCopy },
                { NameParameter, _fullName },
                { NamespaceParameter, _namespace },
            };

            TaskCompletionSource<INavigationResult> completion = new();
            _regionManager.RequestNavigate(
                MainContentRegion,
                nameof(TypeDocumentView),
                result => completion.TrySetResult(result),
                parameters);

            INavigationResult outcome = await completion.Task;
            if (!outcome.Success)
            {
                Status = LocaleManager.Instance.Format(
                    "avalonia.typedoc.status.copyfailed",
                    outcome.Exception?.Message ?? nameof(TypeDocumentView));
            }
        }
        catch (Exception ex)
        {
            Status = LocaleManager.Instance.Format("avalonia.typedoc.status.copyfailed", ex.Message);
        }
        finally
        {
            _isNavigating = false;
        }
    }

    /// <summary>
    /// Returns to the type list, carrying the namespace whose contents should be shown.
    /// </summary>
    private async Task NavigateToListAsync(string? ns)
    {
        if (_isNavigating) return;
        _isNavigating = true;
        try
        {
            string target = string.IsNullOrEmpty(ns)
                ? string.Empty
                : ns;

            NavigationParameters parameters = new()
            {
                { NamespaceParameter, target },
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
                    "avalonia.typedoc.status.navfailed",
                    outcome.Exception?.Message ?? nameof(TypeListView));
            }
        }
        finally
        {
            _isNavigating = false;
        }
    }

    #region Assistant

    private async void ExecuteSendChat()
    {
        SchemaScreenSession? session = _session;
        IAIChat? chat = session?.Chat;
        if (session is null || chat is null || !CanSendChat) return;

        string text = ChatInput.Trim();
        _chatInput = string.Empty;
        RaisePropertyChanged(nameof(ChatInput));
        SendChatCommand.RaiseCanExecuteChanged();

        _chatMessages.Add(new TypeChatMessage(ChatRoleUser, text));
        IsChatBusy = true;

        using CancellationTokenSource cancellation = new();
        _chatCancellation = cancellation;
        try
        {
            AIChatTarget? target = await BuildChatTargetAsync(session);
            if (!IsCurrent(session)) return;

            AIChatRequest request = new()
            {
                UserContent = text,
                ConversationId = _conversationId,
                Target = target,
                // Concrete validation errors from the previous attempt are fed back so the model
                // can correct them instead of repeating the same invalid definition.
                ValidationFeedback = _validationFeedback,
                // The WPF host runs the local implementation; structured results are requested in
                // one non streaming turn so the whole payload can be applied atomically.
                Stream = false,
            };

            AIChatResult? result = null;
            AIChatFailed? failure = null;
            await foreach (AIChatUpdate update in chat.ChatAsync(request, cancellation.Token))
            {
                if (!IsCurrent(session)) return;
                switch (update)
                {
                    case AIChatStarted started when started.ConversationId.Length > 0:
                        _conversationId = started.ConversationId;
                        break;
                    case AIChatCompleted completed:
                        result = completed.Result;
                        break;
                    case AIChatFailed failed:
                        failure = failed;
                        break;
                }
            }

            if (!IsCurrent(session)) return;

            if (failure is not null)
            {
                AddChatError(failure.Message);
                return;
            }

            if (result is null)
            {
                AddChatError(LocaleManager.Instance.Get("avalonia.typedoc.chat.noresult"));
                return;
            }

            if (result.ConversationId.Length > 0)
                _conversationId = result.ConversationId;

            if (!string.IsNullOrWhiteSpace(result.Message))
                _chatMessages.Add(new TypeChatMessage(ChatRoleAssistant, result.Message));

            switch (result.Kind)
            {
                case AIChatResultKind.SchemaChange:
                    await ApplyChatSchemaAsync(session, result);
                    break;
                case AIChatResultKind.ValidationFailed:
                    AddValidationFeedback(result);
                    break;
                case AIChatResultKind.Error:
                    AddChatError(result.Message ?? result.ErrorCode
                                 ?? LocaleManager.Instance.Get("avalonia.typedoc.chat.failed"));
                    break;
                case AIChatResultKind.Chat:
                    if (string.IsNullOrWhiteSpace(result.Message))
                        _chatMessages.Add(new TypeChatMessage(ChatRoleAssistant,
                            LocaleManager.Instance.Get("avalonia.typedoc.chat.nochange")));
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            if (IsCurrent(session))
                _chatMessages.Add(new TypeChatMessage(ChatRoleAssistant,
                    LocaleManager.Instance.Get("avalonia.typedoc.chat.stopped")));
        }
        catch (Exception ex)
        {
            if (IsCurrent(session)) AddChatError(ex.Message);
        }
        finally
        {
            if (ReferenceEquals(_chatCancellation, cancellation)) _chatCancellation = null;
            IsChatBusy = false;
        }
    }

    /// <summary>
    /// Builds the assistant context from the current form content. Serialization is Core work, so
    /// it runs on the thread pool; the result is a snapshot, not a live node reference.
    /// </summary>
    private async Task<AIChatTarget?> BuildChatTargetAsync(SchemaScreenSession session)
    {
        NodeSchema? schema = await Editor.GetNodeSchemaAsync();
        if (schema is null)
            return new AIChatTarget { IsNew = IsNewFlow };

        JsonNode? json = await Task.Run(() => session.Context.ToSchemaJson(schema));
        return new AIChatTarget
        {
            Name = IsNewFlow ? null : _fullName,
            Schema = json,
            IsNew = IsNewFlow,
        };
    }

    /// <summary>
    /// Writes the complete NodeSchema payload back to the definition node. Applying is synchronous
    /// with respect to the conversation: a rejected payload is reported as the concrete editor
    /// error and sent back on the next turn.
    /// </summary>
    private async Task ApplyChatSchemaAsync(SchemaScreenSession session, AIChatResult result)
    {
        if (result.Schema is null)
        {
            AddChatError(LocaleManager.Instance.Get("avalonia.typedoc.chat.noschema"));
            return;
        }

        bool isNew = IsNewFlow;
        if (!IsCurrent(session)) return;

        bool applied = await Editor.ApplySchemaAsync(result.Schema, isNew);
        if (!IsCurrent(session)) return;

        if (!applied)
        {
            string detail = Editor.Error
                            ?? LocaleManager.Instance.Get("avalonia.typedoc.chat.applyfailed");
            _validationFeedback = detail;
            AddChatError(LocaleManager.Instance.Format("avalonia.typedoc.chat.applyfaileddetail", detail));
            return;
        }

        _validationFeedback = null;
        if (!string.IsNullOrWhiteSpace(Editor.TypeName))
            _fullName = Editor.TypeName;

        RaisePropertyChanged(nameof(HeaderText));
        RaisePropertyChanged(nameof(ChatContextText));

        string appliedText = LocaleManager.Instance.Format("avalonia.typedoc.chat.applied", _fullName);
        _chatMessages.Add(new TypeChatMessage(ChatRoleAssistant, appliedText));
        Status = appliedText;
    }

    private void AddValidationFeedback(AIChatResult result)
    {
        string detail = FormatValidationErrors(result);
        _validationFeedback = detail;
        AddChatError(LocaleManager.Instance.Format("avalonia.typedoc.chat.validation", detail));
    }

    private static string FormatValidationErrors(AIChatResult result)
    {
        if (result.Errors.Length == 0)
            return result.Message ?? string.Empty;

        return string.Join(Environment.NewLine, result.Errors.Select(error =>
            (string.IsNullOrWhiteSpace(error.Path) ? string.Empty : error.Path + ": ")
            + error.Code + " - " + error.Message));
    }

    private void AddChatError(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        _chatMessages.Add(new TypeChatMessage(ChatRoleAssistant, message, isError: true));
    }

    private void ExecuteStopChat()
    {
        try
        {
            _chatCancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The turn already completed.
        }
    }

    private void ExecuteClearChat()
    {
        _chatMessages.Clear();
        _conversationId = null;
        _validationFeedback = null;
    }

    /// <summary>
    /// Opens or closes the assistant pane. The pane is an optional split of the definition screen:
    /// closed, the editor keeps the full width; open, the editor keeps the left side and the
    /// assistant takes the right side.
    /// </summary>
    private void ExecuteToggleChat()
    {
        if (!CanShowAiChat) return;
        IsChatVisible = !IsChatVisible;
    }

    /// <summary>
    /// True while the captured session is still the one this screen owns. A navigation that
    /// replaced the session must stop every pending chat continuation from touching UI state.
    /// </summary>
    private bool IsCurrent(SchemaScreenSession session) => ReferenceEquals(_session, session);

    #endregion

    private void Teardown()
    {
        // Cancel assistant work first: it must never write into a session that is being disposed.
        try
        {
            _chatCancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        _chatCancellation = null;
        _chatMessages.Clear();
        _conversationId = null;
        _validationFeedback = null;
        _chatInput = string.Empty;
        _isChatBusy = false;
        _isChatVisible = false;
        RaisePropertyChanged(nameof(ChatInput));
        RaisePropertyChanged(nameof(IsChatBusy));
        RaisePropertyChanged(nameof(CanSendChat));
        RaisePropertyChanged(nameof(HasAiChat));
        RaisePropertyChanged(nameof(CanShowAiChat));
        RaisePropertyChanged(nameof(IsChatVisible));
        SendChatCommand.RaiseCanExecuteChanged();
        StopChatCommand.RaiseCanExecuteChanged();

        Editor.Dispose();
        _session?.Dispose();
        _session = null;
        Status = null;
        IsLoading = false;
    }
}
