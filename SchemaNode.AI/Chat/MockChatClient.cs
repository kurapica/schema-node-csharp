using System.Text.Json;
using Microsoft.Extensions.AI;

namespace SchemaNode.AI.Chat;

/// <summary>
/// The deterministic chat client used by <see cref="SchemaChatProvider.Mock"/>.
/// <para>
/// It implements the model side of the tool loop without contacting any provider: a change
/// request produces a <c>propose_schema_change</c> call carrying a complete, valid NodeSchema
/// payload; the follow-up turn returns a plain summary. Streaming returns text only, so hosts that
/// need structured results should use the non streaming path (the WPF host does).
/// </para>
/// </summary>
public sealed class MockChatClient : IChatClient
{
    private const string ProposalTool = "propose_schema_change";

    /// <summary>
    /// The worked example: a struct with <c>min</c>/<c>max</c> ints and a relation that assigns
    /// <c>max.lowLimit</c> from <c>min</c>.
    /// </summary>
    private const string MinMaxSchema = """
    {
      "namespace": "",
      "name": "minmax",
      "kind": "struct",
      "display": "MinMax",
      "struct": {
        "fields": [
          { "name": "min", "type": "system.int", "display": "Min" },
          { "name": "max", "type": "system.int", "display": "Max" }
        ],
        "relations": [
          {
            "target": "max",
            "property": "lowlimit",
            "kind": "call",
            "call": {
              "args": [ { "type": "system.int", "source": "min" } ],
              "func": "system.intrinsic.assign<system.int>",
              "mode": "call"
            }
          }
        ]
      }
    }
    """;

    private static readonly JsonElement MinMaxSchemaElement =
        JsonDocument.Parse(MinMaxSchema).RootElement.Clone();

    private static readonly string[] ChangeKeywords =
    [
        "create", "add", "make", "generate", "new", "change", "update", "set",
        "创建", "新建", "生成", "添加", "增加", "修改", "设置", "设计",
    ];

    /// <inheritdoc />
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<ChatMessage> list = messages as IReadOnlyList<ChatMessage> ?? messages.ToList();
        string? lastUser = list.LastOrDefault(message => message.Role == ChatRole.User)?.Text;

        if (!HasProposalTool(options) || HasProposalResult(list))
            return Task.FromResult(TextResponse(
                "I proposed the MinMax structure. Review the form and save it when it looks right."));

        if (!LooksLikeChangeRequest(lastUser))
            return Task.FromResult(TextResponse(
                "The mock provider is active. Ask me to create or change a schema; "
                + "for example: create a MinMax struct with min and max integers."));

        Dictionary<string, object?> arguments = new()
        {
            ["schema"] = MinMaxSchemaElement.Clone(),
            ["summary"] = "Created MinMax with min/max ints and max.lowLimit assigned from min.",
        };

        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant,
        [
            new FunctionCallContent($"mock-{Guid.NewGuid():N}", ProposalTool, arguments),
        ])));
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        yield return new ChatResponseUpdate(ChatRole.Assistant,
            "The mock provider streams text only. Use the non streaming call for schema changes.");
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null)
        => serviceType == typeof(IChatClient) ? this : null;

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private static ChatResponse TextResponse(string text)
        => new(new ChatMessage(ChatRole.Assistant, text));

    private static bool HasProposalTool(ChatOptions? options)
        => options?.Tools?.Any(tool => tool is AIFunction function && function.Name == ProposalTool) == true;

    private static bool HasProposalResult(IReadOnlyList<ChatMessage> messages)
        => messages.Any(message => message.Contents?.OfType<FunctionResultContent>().Any() == true);

    private static bool LooksLikeChangeRequest(string? message)
        => message is not null
           && (message.Contains("minmax", StringComparison.OrdinalIgnoreCase)
               || ChangeKeywords.Any(keyword => message.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
}
