using System.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using SchemaNode.AI;
using SchemaNode.AI.Chat;
using SchemaNode.AI.Workflow;
using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using SchemaNode.Service;
using SchemaNode.UnitTest.Base;
using static SchemaNode.Utility.AppConstant;
using ValueType = SchemaNode.Runtime.ValueType;

namespace SchemaNode.UnitTest.AI;

/// <summary>
/// Target type used by the workflow node tests. Non-nullable integers are required fields.
/// </summary>
[Meta<SchemaType>("test.aiminmax")]
public sealed class AIWorkflowTestMinMax
{
    /// <summary>Minimum value.</summary>
    public int Min { get; set; }

    /// <summary>Maximum value.</summary>
    public int Max { get; set; }
}

/// <summary>
/// Focused tests for the AI workflow node's chat client wiring and validation retry loop.
/// No test here touches the network unless it explicitly points at a closed port.
/// </summary>
[TestClass]
public class AIWorkflowNodeTest : CoreTestBase
{
    /// <inheritdoc />
    protected override void ConfigureServices(IServiceCollection services)
    {
        // Make the workflow node and its settings/session schemas discoverable by the engine.
        services.AddAppSchemaAssemblies(typeof(AIWorkflowNode).Assembly);
    }

    [TestMethod]
    public async Task WorkflowNode_IsDiscoveredAsWorkflowKind()
    {
        var nodeType = await Context.GetNodeTypeAsync("system.workflow.ai.generate");
        Assert.IsNotNull(nodeType);
        Assert.AreEqual(NODE_KIND_WORKFLOW, nodeType.Kind);
        Assert.IsNotNull(await Context.GetNodeTypeAsync("system.schema.prop.workflow.ai.target"));
        Assert.IsNotNull(await Context.GetNodeTypeAsync("system.workflow.ai.settings"));
    }

    [TestMethod]
    public async Task RetryLoop_FeedsLastValidationErrorIntoNextRequest()
    {
        ValueType target = (await Context.GetNodeTypeAsync<ValueType>("test.aiminmax"))!;
        ScriptedChatClient client = new("{\"min\":\"x\",\"max\":2}", "{\"min\":1,\"max\":2}");

        AIWorkflowRunResult result = await AIWorkflowRunner.RunAsync(
            Context,
            client,
            new SchemaChatOptions { RequestTimeoutSeconds = 30 },
            new AIWorkflowRunRequest
            {
                TargetType = target,
                MaxRetries = 1,
                Instruction = "Return a minmax object.",
            });

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(2, result.CallCount);
        Assert.IsNotNull(result.Attempts[0].Error);

        string retryPrompt = client.Requests[1][^1].Text ?? string.Empty;
        StringAssert.Contains(retryPrompt, "rejected");
        StringAssert.Contains(retryPrompt, result.Attempts[0].Error!);
        StringAssert.Contains(retryPrompt, "\"min\":\"x\"");
    }

    [TestMethod]
    public async Task RetryLoop_ExhaustionCarriesRealLastError()
    {
        ValueType target = (await Context.GetNodeTypeAsync<ValueType>("test.aiminmax"))!;
        ScriptedChatClient client = new("{\"min\":\"x\",\"max\":2}");

        AIWorkflowRunResult result = await AIWorkflowRunner.RunAsync(
            Context,
            client,
            new SchemaChatOptions { RequestTimeoutSeconds = 30 },
            new AIWorkflowRunRequest
            {
                TargetType = target,
                MaxRetries = 1,
            });

        Assert.IsFalse(result.Succeeded);
        Assert.IsNull(result.Value);
        Assert.AreEqual(2, result.CallCount);
        Assert.IsNotNull(result.Error);
        Assert.AreEqual(result.Attempts[^1].Error, result.Error);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.Error));
    }

    [TestMethod]
    public async Task ClosedPortClient_FailsInsteadOfHanging()
    {
        using IChatClient client = SchemaNodeChatInjection.CreateClient(new SchemaChatOptions
        {
            Provider = SchemaChatProvider.Ollama,
            Endpoint = "http://127.0.0.1:1",
            ModelId = "qwen3:8b",
            RequestTimeoutSeconds = 2,
        });
        Stopwatch watch = Stopwatch.StartNew();

        await Assert.ThrowsAsync<Exception>(async () =>
            await client.GetResponseAsync(
                [new ChatMessage(ChatRole.User, "hello")],
                new ChatOptions { ModelId = "qwen3:8b" }));

        watch.Stop();
        Assert.IsTrue(watch.Elapsed < TimeSpan.FromSeconds(20), $"The unreachable client took {watch.Elapsed}.");
    }

    /// <summary>
    /// Deliberately small live smoke test. It is inert unless <c>SCHEMANODE_AI_SMOKE=1</c>.
    /// It asks for a wrong-typed <c>min</c> value on the first attempt, so validation fails
    /// and one retry must carry the real error back to the model.
    /// </summary>
    [TestMethod]
    public async Task OllamaSmoke_ForcedRetryRoundTrip()
    {
        if (Environment.GetEnvironmentVariable("SCHEMANODE_AI_SMOKE") != "1")
            return;

        ValueType target = (await Context.GetNodeTypeAsync<ValueType>("test.aiminmax"))!;
        SchemaChatOptions options = new()
        {
            Provider = SchemaChatProvider.Ollama,
            Endpoint = "http://localhost:11434",
            ModelId = "qwen3:8b",
            RequestTimeoutSeconds = 180,
            MaxOutputTokens = 160,
            Temperature = 0.0f,
        };
        using IChatClient client = SchemaNodeChatInjection.CreateClient(options);
        Stopwatch watch = Stopwatch.StartNew();

        AIWorkflowRunResult result = await AIWorkflowRunner.RunAsync(
            Context,
            client,
            options,
            new AIWorkflowRunRequest
            {
                TargetType = target,
                MaxRetries = 1,
                Instruction =
                    "Return exactly {\"min\":\"x\",\"max\":2} on the first attempt. " +
                    "If the conversation contains a rejection message, return {\"min\":1,\"max\":2} instead.",
            });

        watch.Stop();
        TestContext.WriteLine($"elapsed: {watch.Elapsed}");
        TestContext.WriteLine($"calls: {result.CallCount}");
        TestContext.WriteLine($"first raw: {result.Attempts.FirstOrDefault()?.RawOutput}");
        TestContext.WriteLine($"first error: {result.Attempts.FirstOrDefault()?.Error}");
        TestContext.WriteLine($"last raw: {result.Attempts.LastOrDefault()?.RawOutput}");
        TestContext.WriteLine($"succeeded: {result.Succeeded}, error: {result.Error}");

        Assert.IsTrue(result.Succeeded, $"The live model did not produce a valid value. Last error: {result.Error}");
    }

    /// <inheritdoc />
    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// Returns the scripted responses in order and records each request for assertions. The last
    /// response is repeated when the script is exhausted.
    /// </summary>
    private sealed class ScriptedChatClient(params string[] responses) : IChatClient
    {
        private int _index;

        public List<IReadOnlyList<ChatMessage>> Requests { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(messages.ToList());
            string text = responses[Math.Min(_index++, responses.Length - 1)];
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
