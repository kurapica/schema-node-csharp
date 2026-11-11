using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchemaNode.AI;
using SchemaNode.AI.Chat;
using SchemaNode.Schema.Validation;

namespace SchemaNode.UnitTest.AI;

/// <summary>
/// Verifies the namespace immutability rule: a candidate that changes the namespace supplied by
/// the editor is rejected instead of being silently rewritten. The model is replaced by a stub so
/// the test is deterministic and needs no live provider.
/// </summary>
[TestClass]
public class AIChatNamespaceRuleTest : Base.AppTestBase
{
    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);

        services.AddSchemaChat(options =>
        {
            options.Provider = SchemaChatProvider.Mock;
            options.ModelId = "mock";
            options.MaxIterationsPerRequest = 4;
            options.RequestTimeoutSeconds = 30;
        });

        // Replace the provider client after registration; SchemaChatService resolves it lazily.
        services.Replace(ServiceDescriptor.Singleton<IChatClient>(new ChangedNamespaceChatClient()));
    }

    [TestMethod]
    public async Task LocalChat_NamespaceChangedByModel_ReturnsValidationFailure()
    {
        using IServiceScope scope = Provider.CreateScope();
        IAIChat chat = scope.ServiceProvider.GetRequiredService<IAIChat>();

        AIChatResult? result = null;
        await foreach (AIChatUpdate update in chat.ChatAsync(new AIChatRequest
        {
            UserContent = "Add a MinMax struct with min and max integers.",
            Target = new AIChatTarget
            {
                Schema = JsonNode.Parse("""{"namespace":"demo","name":"","kind":"struct"}"""),
                IsNew = true,
            },
        }))
        {
            if (update is AIChatCompleted completed)
                result = completed.Result;
        }

        Assert.IsNotNull(result);
        Assert.AreEqual(AIChatResultKind.ValidationFailed, result!.Kind);

        // The candidate is reported, never rewritten: the model's namespace survives in the result.
        Assert.AreEqual("wrong", result.Schema?["namespace"]?.GetValue<string>());
        Assert.IsTrue(result.Errors.Any(error => error.Code == SchemaValidationErrorCodes.NamespaceImmutable),
            "The namespace rule must be reported as a concrete validation error.");
    }

    /// <summary>Proposes a schema with a namespace that differs from the editor.</summary>
    private sealed class ChangedNamespaceChatClient : IChatClient
    {
        private const string Payload = """
        {
          "namespace": "wrong",
          "name": "minmax",
          "kind": "struct",
          "display": "MinMax",
          "struct": {
            "fields": [
              { "name": "min", "type": "system.int", "display": "Min" },
              { "name": "max", "type": "system.int", "display": "Max" }
            ]
          }
        }
        """;

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            bool toolRan = messages.Any(message => message.Contents.Any(content => content is FunctionResultContent));
            if (!toolRan && options?.Tools?.Any(tool => tool.Name == SchemaChatPrompt.ToolProposeChange) == true)
            {
                FunctionCallContent call = new("namespace-call", SchemaChatPrompt.ToolProposeChange,
                    new Dictionary<string, object?>
                    {
                        ["schema"] = JsonDocument.Parse(Payload).RootElement.Clone(),
                        ["summary"] = "Changed the namespace.",
                    });
                return Task.FromResult(new ChatResponse(
                    new ChatMessage(ChatRole.Assistant, [call])));
            }

            return Task.FromResult(new ChatResponse(
                new ChatMessage(ChatRole.Assistant, "The candidate failed validation.")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The namespace rule test does not stream.");

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
