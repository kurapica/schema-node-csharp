using System.Text.Json;
using System.Text.Json.Nodes;
using SchemaNode.AI.Agents;
using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Workflow;
using static SchemaNode.Utility.AppConstant;
using ValueType = SchemaNode.Runtime.ValueType;

namespace SchemaNode.AI.Workflow;

/// <summary>
/// Generates a JSON value with an agent provider, validates it against a declared target schema
/// type, and retries with the concrete validation error until the value is valid or the retry
/// budget is exhausted. The validated value becomes the node payload.
/// </summary>
[Meta<SchemaType>("system.workflow.ai.generate")]
[Meta<OfNodeKind>(NODE_KIND_WORKFLOW)]
public sealed class AIWorkflowNode
    : BaseWorkflow, IWorkflowSettings<AIWorkflowSettings>, IWorkflowSession<AIWorkflowSession>, IWorkflowPayload<JsonNode>
{
    private static readonly JsonSerializerOptions SettingsJson = new(JsonSerializerDefaults.Web);
    private string? _settingsError;
    private string? _target;

    /// <inheritdoc />
    public AIWorkflowSettings? Settings { get; set; }

    /// <inheritdoc />
    public override Task LoadAsync(SchemaContext context, AppWorkflowNodeSchema schema)
    {
        _target = schema.GetProperty<AITargetProperty>()?.Value?.Trim();

        if (schema.State is { } state)
        {
            try
            {
                Settings = state.Deserialize<AIWorkflowSettings>(SettingsJson) ?? new AIWorkflowSettings();
            }
            catch (Exception ex)
            {
                _settingsError = ex.Message;
                Settings = new AIWorkflowSettings();
            }
        }

        Settings ??= new AIWorkflowSettings();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Generates and validates the value.
    /// </summary>
    /// <param name="context">The workflow context.</param>
    /// <param name="session">The node session, or null on the first call.</param>
    /// <param name="prompt">Optional text bound to the node's <c>prompt</c> argument.</param>
    /// <param name="input">
    /// Optional current workflow context object bound to the node's <c>input</c> argument.
    /// Prompt sources of kind <c>Context</c> serialize this object.
    /// </param>
    /// <returns>The updated session.</returns>
    public async Task<AIWorkflowSession?> ProcessAsync(
        WorkflowContext context,
        AIWorkflowSession? session,
        string? prompt = null,
        JsonNode? input = null)
    {
        session ??= new AIWorkflowSession();
        AIWorkflowSettings settings = Settings ?? new AIWorkflowSettings();

        if (_settingsError is not null)
        {
            context.Error(this, $"The AI workflow settings are invalid: {_settingsError}");
            return session;
        }

        string? targetName = _target;
        if (string.IsNullOrWhiteSpace(targetName))
        {
            context.Error(this, "AI workflow requires the AI target property, for example 'test.minmax'.");
            return session;
        }

        ValueType? targetType = await context.GetNodeTypeAsync<ValueType>(targetName).ConfigureAwait(false);
        if (targetType is null)
        {
            context.Error(this, $"The AI workflow target schema type '{targetName}' could not be resolved.");
            return session;
        }

        AIAgentProviderFactory factory = context.GetService<AIAgentProviderFactory>() ?? AIAgentProviderFactory.Default;
        AIAgentOptions defaults = factory.Options;
        int maxRetries = Math.Clamp(settings.MaxRetries ?? defaults.MaxRetries, 0, 10);
        int timeoutSeconds = Math.Clamp(settings.TimeoutSeconds ?? defaults.RequestTimeoutSeconds, 1, 3600);
        int maxTokens = Math.Clamp(settings.MaxTokens ?? defaults.MaxTokens, 1, 8192);
        int maxMetadataChars = Math.Max(0, settings.MaxMetadataChars ?? defaults.MaxMetadataChars);
        double temperature = settings.Temperature ?? defaults.Temperature;

        string[]? mockResponses = settings.MockResponses?
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        IAgentProvider provider = factory.Create(
            settings.Provider,
            settings.Endpoint,
            settings.Model,
            timeoutSeconds,
            mockResponses);

        List<string> blocks = BuildPromptBlocks(context, settings, prompt, input);
        AIWorkflowRunResult result;
        try
        {
            result = await AIWorkflowRunner.RunAsync(
                context,
                new AIWorkflowRunRequest
                {
                    TargetType = targetType,
                    Provider = provider,
                    MaxRetries = maxRetries,
                    SystemPrompt = settings.SystemPrompt,
                    Instruction = settings.Instruction,
                    PromptBlocks = blocks,
                    MaxTokens = maxTokens,
                    Temperature = temperature,
                    MaxMetadataChars = maxMetadataChars,
                },
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (AIAgentException ex)
        {
            session.LastError = ex.Message;
            context.Error(this, ex.Message);
            return session;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            session.LastError = ex.Message;
            context.Error(this, $"AI workflow failed: {ex.Message}");
            return session;
        }

        session.Attempts += result.CallCount;
        if (result.Succeeded)
        {
            session.LastError = null;
            context.Done(this, result.Value);
            return session;
        }

        session.LastError = result.Error;
        context.Error(
            this,
            $"AI generation failed after {result.CallCount} attempt(s) using provider '{provider.Name}'. " +
            $"Last validation error: {result.Error}");
        return session;
    }

    /// <inheritdoc />
    public Task ReleaseSessionAsync(WorkflowContext context, AIWorkflowSession? session) => Task.CompletedTask;

    private static List<string> BuildPromptBlocks(
        WorkflowContext context,
        AIWorkflowSettings settings,
        string? prompt,
        JsonNode? input)
    {
        List<string> blocks = [];

        if (!string.IsNullOrWhiteSpace(prompt))
            blocks.Add("Call prompt:\n" + prompt.Trim());

        bool hasContextSource = false;
        foreach (AIWorkflowPromptSource source in settings.Prompts ?? [])
        {
            switch (source.Kind)
            {
                case AIWorkflowPromptSourceKind.Text:
                    if (!string.IsNullOrWhiteSpace(source.Text))
                        blocks.Add(FormatBlock(source.Label ?? "Instruction", source.Text));
                    break;

                case AIWorkflowPromptSourceKind.Context:
                    hasContextSource = true;
                    if (input is not null)
                        blocks.Add(FormatBlock(source.Label ?? "Current workflow context", input.ToJsonString()));
                    break;

                case AIWorkflowPromptSourceKind.Payload:
                    string sourceName = source.Node?.Trim() ?? string.Empty;
                    if (sourceName.Length == 0) break;

                    string path = source.Path?.Trim() ?? string.Empty;
                    string access = path.Length == 0 ? sourceName : $"{sourceName}.{path}";
                    IValueAccess? payload = context.GetWorkflowPayload(access);
                    if (payload is null)
                    {
                        blocks.Add($"[payload '{access}' is unavailable]");
                        break;
                    }

                    string? text = RenderPayload(payload);
                    if (!string.IsNullOrWhiteSpace(text))
                        blocks.Add(FormatBlock(source.Label ?? $"Payload '{access}'", text));
                    break;
            }
        }

        if (input is not null && !hasContextSource)
            blocks.Add(FormatBlock("Current workflow context", input.ToJsonString()));

        return blocks;
    }

    private static string FormatBlock(string label, string content)
        => $"{label}:\n{content.Trim()}";

    private static string? RenderPayload(IValueAccess payload)
    {
        if (payload.TryGetOriginal<JsonNode>(out JsonNode? original) && original is not null)
            return original.ToJsonString();

        if (payload.TryGetValue<JsonNode>(out JsonNode? value) && value is not null)
            return value.ToJsonString();

        if (payload.TryGetValue<object>(out object? raw) && raw is not null)
            return raw is JsonNode json ? json.ToJsonString() : JsonSerializer.Serialize(raw);

        return payload.ToString();
    }
}
