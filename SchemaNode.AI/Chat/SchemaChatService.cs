using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using SchemaNode.Context;
using SchemaNode.Schema;
using SchemaNode.Schema.Validation;

namespace SchemaNode.AI.Chat;

/// <summary>
/// Orchestrates the schema assistant conversation.
/// <para>
/// The service uses <see cref="FunctionInvokingChatClient"/> to run the model/tool loop. The
/// read tools are built from the existing MCP tools and the terminal tool captures the structured
/// candidate. The final <see cref="ChatSchemaResponse"/> never contains raw tool output.
/// </para>
/// </summary>
public sealed class SchemaChatService
{
    private readonly SchemaContext _context;
    private readonly SchemaChatOptions _options;
    private readonly SchemaChatConversationStore _store;
    private readonly IChatClient _chatClient;
    private readonly IServiceProvider _services;
    private readonly ILoggerFactory _loggerFactory;
    private readonly SchemaChatSession _session;
    private readonly ILogger<SchemaChatService> _logger;
    private string? _agentFailureMessage;

    /// <summary>Creates the service.</summary>
    public SchemaChatService(
        SchemaContext context,
        SchemaChatOptions options,
        SchemaChatConversationStore store,
        IChatClient chatClient,
        IServiceProvider services,
        ILoggerFactory loggerFactory,
        SchemaChatSession session,
        ILogger<SchemaChatService> logger)
    {
        _context = context;
        _options = options;
        _store = store;
        _chatClient = chatClient;
        _services = services;
        _loggerFactory = loggerFactory;
        _session = session;
        _logger = logger;
    }

    /// <summary>
    /// Executes one turn and streams text deltas, ending with a final completed response.
    /// </summary>
    public async IAsyncEnumerable<SchemaChatUpdate> ExecuteStreamAsync(
        ChatSchemaRequest request,
        string owner,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (SchemaChatUpdate update in RunAsync(request, owner, stream: true, cancellationToken))
            yield return update;
    }

    /// <summary>
    /// Executes one turn without streaming and returns the final completed response.
    /// </summary>
    public async IAsyncEnumerable<SchemaChatUpdate> ExecuteAsync(
        ChatSchemaRequest request,
        string owner,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (SchemaChatUpdate update in RunAsync(request, owner, stream: false, cancellationToken))
            yield return update;
    }

    private async IAsyncEnumerable<SchemaChatUpdate> RunAsync(
        ChatSchemaRequest request,
        string owner,
        bool stream,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _agentFailureMessage = null;

        string conversationId = SchemaChatConversationStore.IsValidId(request.ConversationId)
            ? request.ConversationId!
            : SchemaChatConversationStore.NewId();

        yield return new SchemaChatStarted(conversationId);

        // ── Validate the request and build the editor context ─────────────────────
        string? errorCode = null;
        string? errorMessage = null;
        string? target = null;
        NodeSchema? currentSchema = null;
        JsonNode? currentJson = null;

        try
        {
            (target, currentSchema, currentJson) = await PrepareContextAsync(request, cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            errorCode = "not_authorized";
            errorMessage = "The current user is not allowed to read the target schema.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Chat] Failed to prepare the schema chat context.");
            errorCode = "invalid_context";
            errorMessage = $"The editor context is invalid: {ex.Message}";
        }

        if (errorCode != null)
        {
            yield return new SchemaChatCompleted(BuildError(conversationId, errorCode, errorMessage!));
            yield break;
        }

        _session.Target = target;

        // ── Build messages from system prompt + bounded history + new user message ─
        List<ChatMessage> messages = [];
        string prompt = SchemaChatPrompt.Build(request.Locale, target, currentJson, request.IsNew, _options);
        messages.Add(new ChatMessage(ChatRole.System, prompt));
        messages.AddRange(_store.Get(owner, conversationId));
        messages.Add(new ChatMessage(ChatRole.User, request.Message.Trim()));
        _store.Append(owner, conversationId, new ChatMessage(ChatRole.User, request.Message.Trim()));

        IList<AITool> tools = SchemaChatToolset.Create(_context, _session, target, request.IsNew == true, currentJson);
        var chatOptions = new ChatOptions
        {
            Tools = tools,
            Temperature = _options.Temperature,
            ModelId = _options.ModelId,
            AllowMultipleToolCalls = true,
        };
        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (_options.RequestTimeoutSeconds > 0)
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.RequestTimeoutSeconds));
        CancellationToken token = timeoutCts.Token;

        FunctionInvokingChatClient client = CreateFunctionClient();

        // FunctionInvokingChatClient may append tool messages to the input list. Keep a clean copy
        // for retrying when the model returns an empty turn.
        List<ChatMessage> cleanMessages = messages.ToList();

        ChatSchemaResponse response;
        if (stream)
        {
            StringBuilder streamedText = new();
            string? failure = null;

            await using IAsyncEnumerator<ChatResponseUpdate> enumerator =
                client.GetStreamingResponseAsync(messages, chatOptions, token).GetAsyncEnumerator(token);

            while (true)
            {
                bool hasNext;
                try
                {
                    hasNext = await enumerator.MoveNextAsync();
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (OperationCanceledException)
                {
                    failure = token.IsCancellationRequested && !cancellationToken.IsCancellationRequested
                        ? "timeout"
                        : "agent_error";
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Chat] The streaming schema assistant failed.");
                    _agentFailureMessage = ex.Message;
                    failure = "agent_error";
                    break;
                }

                if (!hasNext) break;

                string? delta = enumerator.Current.Text;
                if (string.IsNullOrEmpty(delta)) continue;

                streamedText.Append(delta);
                yield return new SchemaChatTextDelta(delta);
            }

            if (token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                yield return new SchemaChatCompleted(BuildError(
                    conversationId,
                    "timeout",
                    $"The assistant did not answer within {_options.RequestTimeoutSeconds} seconds."));
                yield break;
            }

            response = BuildResponse(conversationId, streamedText.ToString(), currentSchema, currentJson);
            ApplyStreamingFailure(response, failure);
        }
        else
        {
            string text = string.Empty;
            string? failure = null;
            try
            {
                ChatResponse chatResponse = await client.GetResponseAsync(messages, chatOptions, token);
                text = chatResponse.Text ?? string.Empty;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                failure = token.IsCancellationRequested && !cancellationToken.IsCancellationRequested
                    ? "timeout"
                    : "agent_error";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Chat] The schema assistant failed.");
                _agentFailureMessage = ex.Message;
                failure = "agent_error";
            }

            if (token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                yield return new SchemaChatCompleted(BuildError(
                    conversationId,
                    "timeout",
                    $"The assistant did not answer within {_options.RequestTimeoutSeconds} seconds."));
                yield break;
            }

            response = BuildResponse(conversationId, text, currentSchema, currentJson);
            ApplyStreamingFailure(response, failure);
        }

        if (response.Kind == SchemaChatResultKind.Error && response.ErrorCode == "empty_response"
            && !token.IsCancellationRequested)
        {
            _logger.LogWarning("[Chat] Empty assistant turn; retrying with an explicit instruction.");
            response = await RetryEmptyTurnAsync(
                conversationId,
                cleanMessages,
                chatOptions,
                target,
                currentSchema,
                currentJson,
                request.Message,
                token,
                cancellationToken);
        }

        if (response.Kind == SchemaChatResultKind.Error && response.ErrorCode == "empty_response"
            && !token.IsCancellationRequested && currentJson != null && LooksLikeChangeRequest(request.Message))
        {
            _logger.LogWarning("[Chat] Tool loop returned no answer; trying the direct JSON fallback.");
            ChatSchemaResponse? fallback = await TryDirectJsonAsync(
                conversationId,
                request.Message,
                target,
                currentSchema,
                currentJson,
                token,
                cancellationToken);
            if (fallback != null) response = fallback;
        }

        bool noChange = response.Kind == SchemaChatResultKind.ValidationFailed
                        && response.Change?.Errors.Any(error => error.Code == SchemaValidationErrorCodes.NoChange) == true;
        if ((response is { Kind: SchemaChatResultKind.Error, ErrorCode: "empty_response" } || noChange)
            && LooksLikeLocalizationRequest(request.Message)
            && currentJson != null
            && !token.IsCancellationRequested)
        {
            _logger.LogWarning("[Chat] Applying the deterministic display localization fallback.");
            response = await TryLocalizationFallbackAsync(
                conversationId,
                target,
                currentSchema,
                currentJson,
                response,
                token);
        }

        if (!string.IsNullOrWhiteSpace(response.Message))
            _store.Append(owner, conversationId, new ChatMessage(ChatRole.Assistant, response.Message));

        yield return new SchemaChatCompleted(response);
    }

    private FunctionInvokingChatClient CreateFunctionClient()
    {
        FunctionInvokingChatClient client = new(_chatClient, _loggerFactory, _services)
        {
            MaximumIterationsPerRequest = Math.Max(1, _options.MaxIterationsPerRequest),
            MaximumConsecutiveErrorsPerRequest = 2,
            IncludeDetailedErrors = true,
        };
        client.FunctionInvoker = InvokeToolAsync;
        return client;
    }

    private async ValueTask<object?> InvokeToolAsync(FunctionInvocationContext invocation, CancellationToken cancellationToken)
    {
        string name = invocation.CallContent.Name;
        _session.RecordToolCall(name);

        try
        {
            return await invocation.Function.InvokeAsync(invocation.Arguments, cancellationToken);
        }
        catch (Exception ex)
        {
            _session.RecordToolError(name, ex.Message);
            throw;
        }
    }

    private void ApplyStreamingFailure(ChatSchemaResponse response, string? failure)
    {
        if (failure == null) return;
        if (response.Kind is SchemaChatResultKind.SchemaChange or SchemaChatResultKind.ValidationFailed) return;

        response.Kind = SchemaChatResultKind.Error;
        response.ErrorCode = failure;
        response.Message = failure == "timeout"
            ? $"The assistant did not answer within {_options.RequestTimeoutSeconds} seconds."
            : $"The assistant failed: {_agentFailureMessage ?? "unknown error"}";
    }

    private static readonly string[] ChangeKeywords =
    [
        "创建", "新建", "增加", "添加", "修改", "改成", "改为", "调整", "设置", "删除", "移除", "重命名",
        "字段", "属性", "翻译", "display", "create", "add", "update", "change", "adjust", "set",
        "remove", "delete", "rename", "field", "property", "translate",
    ];

    private static bool LooksLikeChangeRequest(string message)
        => ChangeKeywords.Any(keyword => message.Contains(keyword, StringComparison.OrdinalIgnoreCase));

    private static bool LooksLikeLocalizationRequest(string message)
        => new[] { "display", "翻译", "英文", "zhCN", "中文", "label" }
            .Any(keyword => message.Contains(keyword, StringComparison.OrdinalIgnoreCase));

    private async Task<ChatSchemaResponse> RetryEmptyTurnAsync(
        string conversationId,
        List<ChatMessage> cleanMessages,
        ChatOptions chatOptions,
        string? target,
        NodeSchema? currentSchema,
        JsonNode? currentJson,
        string userMessage,
        CancellationToken token,
        CancellationToken clientToken)
    {
        List<ChatMessage> retryMessages = [.. cleanMessages];
        string retryInstruction = LooksLikeLocalizationRequest(userMessage)
            ? "Your previous response was empty. The user asked to change display labels. " +
              "Rewrite EVERY display object as { \"key\": \"English label\", \"trans\": [ { \"lang\": \"zhCN\", \"tran\": \"Chinese label\" } ] }, " +
              "including the schema display and every field/value/argument display. " +
              "The candidate must differ from the current schema. Call propose_schema_change now with the complete schema."
            : "Your previous response was empty. Respond now. " +
              "If the user asked to create or modify a schema, call propose_schema_change with the complete schema now; " +
              "otherwise answer the user's question in natural language.";
        retryMessages.Add(new ChatMessage(ChatRole.User, retryInstruction));

        ChatOptions retryOptions = chatOptions.Clone();
        string retryText = string.Empty;
        try
        {
            ChatResponse retryResponse = await CreateFunctionClient().GetResponseAsync(retryMessages, retryOptions, token);
            retryText = retryResponse.Text ?? string.Empty;
        }
        catch (OperationCanceledException) when (clientToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Chat] The empty-turn retry failed.");
            _agentFailureMessage = ex.Message;
        }

        if (token.IsCancellationRequested && !clientToken.IsCancellationRequested)
            return BuildError(conversationId, "timeout",
                $"The assistant did not answer within {_options.RequestTimeoutSeconds} seconds.");

        return BuildResponse(conversationId, retryText, currentSchema, currentJson);
    }

    /// <summary>
    /// Last resort for small models that reason correctly but never emit a tool call: ask for the
    /// complete updated schema as plain JSON, then validate and wrap it as a normal candidate.
    /// </summary>
    private async Task<ChatSchemaResponse?> TryDirectJsonAsync(
        string conversationId,
        string userMessage,
        string? target,
        NodeSchema? currentSchema,
        JsonNode? currentJson,
        CancellationToken token,
        CancellationToken clientToken)
    {
        if (currentJson == null) return null;

        bool localization = LooksLikeLocalizationRequest(userMessage);
        string schemaText = currentJson.ToJsonString(SchemaChatJson.Options);
        string prompt = $$"""
            You are a SchemaNode schema editor. Update the current node schema according to the user's request.
            Return ONLY the complete updated node schema JSON. Do not use markdown, code fences or explanations.
            Keep namespace, name and kind unchanged. Preserve every field and property the user did not ask to change.

            A locale string is:
            { "key": "English default", "trans": [ { "lang": "zhCN", "tran": "Chinese translation" } ] }

            Current schema:
            {{schemaText}}
            """;

        if (localization)
        {
            prompt += """


                The user wants English defaults with Chinese translations.
                Rewrite EVERY display object, including the schema display and every field display:
                { "key": "English label", "trans": [ { "lang": "zhCN", "tran": "original Chinese label" } ] }
                Never leave trans empty and never use "language" or "text".
                """;
        }

        string text;
        try
        {
            ChatResponse response = await _chatClient.GetResponseAsync(
                [
                    new ChatMessage(ChatRole.System, prompt),
                    new ChatMessage(ChatRole.User, userMessage),
                ],
                new ChatOptions { Temperature = _options.Temperature, ModelId = _options.ModelId },
                token);
            text = response.Text ?? string.Empty;
        }
        catch (OperationCanceledException) when (clientToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Chat] The direct JSON fallback failed.");
            return null;
        }

        string? json = ExtractJsonObject(text);
        if (json == null) return null;

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }

        SchemaValidationResult validation = await _context.ValidateNodeSchemaAsync(
            node, target, checkAuthorization: true, token);

        _session.Proposal = new SchemaChangeCandidate
        {
            Operation = validation.Operation
                ?? (string.IsNullOrWhiteSpace(target)
                    ? SchemaCandidateValidator.OperationCreate
                    : SchemaCandidateValidator.OperationUpdate),
            TargetName = validation.TargetName ?? target ?? string.Empty,
            Schema = validation.Schema,
            Normalized = validation.Normalized,
            Summary = "Applied the requested schema change.",
            IsValid = validation.IsValid,
            Errors = validation.Errors.ToArray(),
            Warnings = validation.Warnings.ToArray(),
        };
        _session.ProposalCount++;

        return BuildResponse(conversationId, text, currentSchema, currentJson);
    }

    private async Task<ChatSchemaResponse> TryLocalizationFallbackAsync(
        string conversationId,
        string? target,
        NodeSchema? currentSchema,
        JsonNode currentJson,
        ChatSchemaResponse original,
        CancellationToken token)
    {
        JsonNode? localized = SchemaChatJson.LocalizeDisplays(currentJson);
        if (localized == null) return original;

        SchemaValidationResult validation = await _context.ValidateNodeSchemaAsync(
            localized, target, checkAuthorization: true, token);
        if (!validation.IsValid) return original;

        _session.Proposal = new SchemaChangeCandidate
        {
            Operation = validation.Operation
                ?? (string.IsNullOrWhiteSpace(target)
                    ? SchemaCandidateValidator.OperationCreate
                    : SchemaCandidateValidator.OperationUpdate),
            TargetName = validation.TargetName ?? target ?? string.Empty,
            Schema = validation.Schema,
            Normalized = validation.Normalized,
            Summary = "Updated display labels to English defaults with zhCN translations.",
            IsValid = true,
            Errors = [],
            Warnings = validation.Warnings.ToArray(),
        };
        _session.ProposalCount++;

        return BuildResponse(
            conversationId,
            "已将 display 更新为英文默认，并补充中文（zhCN）翻译。",
            currentSchema,
            currentJson);
    }

    private static string? ExtractJsonObject(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        int start = text.IndexOf('{');
        int end = text.LastIndexOf('}');
        if (start < 0 || end <= start) return null;
        return text[start..(end + 1)];
    }

    private async Task<(string? Target, NodeSchema? Schema, JsonNode? Json)> PrepareContextAsync(
        ChatSchemaRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            throw new InvalidOperationException("The message is required.");
        if (request.Message.Length > _options.MaxMessageChars)
            throw new InvalidOperationException($"The message exceeds {_options.MaxMessageChars} characters.");

        string? target = string.IsNullOrWhiteSpace(request.Target) ? null : request.Target.Trim();
        JsonNode? currentJson = request.CurrentSchema;
        NodeSchema? currentSchema = null;

        if (currentJson != null)
        {
            string raw = currentJson.ToJsonString();
            if (raw.Length > _options.MaxContextSchemaChars)
                throw new InvalidOperationException($"The current schema exceeds {_options.MaxContextSchemaChars} characters.");

            currentSchema = _context.ParseNodeSchema(currentJson)
                ?? throw new InvalidOperationException("The current editor content is not a valid node schema.");

            currentJson = SchemaChatJson.NormalizeForPrompt(currentJson);

            string candidateName = string.IsNullOrWhiteSpace(currentSchema.Name)
                ? string.Empty
                : currentSchema.FullName;
            if (!string.IsNullOrWhiteSpace(candidateName))
            {
                if (target == null)
                    target = candidateName;
                else if (!target.Equals(candidateName, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        $"The target '{target}' does not match the current editor content '{candidateName}'.");
            }
        }

        if (currentSchema == null && !string.IsNullOrWhiteSpace(target))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var nodeType = await _context.GetNodeTypeAsync(target);
            if (nodeType != null)
            {
                currentSchema = await _context.GetNodeSchemaAsync(nodeType)
                    ?? throw new UnauthorizedAccessException();
                currentJson = SchemaChatJson.NormalizeForPrompt(_context.ToSchemaJson(currentSchema));
            }
        }

        return (target, currentSchema, currentJson);
    }

    private ChatSchemaResponse BuildResponse(
        string conversationId,
        string text,
        NodeSchema? currentSchema,
        JsonNode? currentJson)
    {
        var response = new ChatSchemaResponse
        {
            ConversationId = conversationId,
            ToolCalls = _session.ToolCalls.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
        };

        if (_session.Proposal is { } proposal)
        {
            JsonNode? before = currentJson ?? _context.ToSchemaJson(currentSchema);
            JsonNode? after = proposal.Normalized ?? _context.ToSchemaJson(proposal.Schema);
            proposal.Changes = SchemaChatJson.Diff(before, after);

            response.Change = proposal;
            response.Kind = proposal.IsValid
                ? SchemaChatResultKind.SchemaChange
                : SchemaChatResultKind.ValidationFailed;
            response.Message = !string.IsNullOrWhiteSpace(text)
                ? text
                : proposal.Summary ?? (proposal.IsValid
                    ? "The schema candidate is valid."
                    : "The proposed schema failed validation.");

            if (!proposal.IsValid)
                response.ErrorCode = proposal.Errors.FirstOrDefault()?.Code;

            return response;
        }

        if (!string.IsNullOrWhiteSpace(text))
        {
            response.Kind = SchemaChatResultKind.Chat;
            response.Message = text.Trim();
            return response;
        }

        if (_session.ToolErrors.Count > 0)
        {
            response.Kind = SchemaChatResultKind.Error;
            response.ErrorCode = "tool_error";
            response.Message = "A tool invocation failed: " + string.Join("; ", _session.ToolErrors.Take(3));
            return response;
        }

        response.Kind = SchemaChatResultKind.Error;
        response.ErrorCode = "empty_response";
        response.Message = "The assistant returned no answer.";
        return response;
    }

    private static ChatSchemaResponse BuildError(string conversationId, string code, string message) => new()
    {
        ConversationId = conversationId,
        Kind = SchemaChatResultKind.Error,
        ErrorCode = code,
        Message = message,
    };
}
