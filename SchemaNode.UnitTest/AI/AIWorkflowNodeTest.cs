using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SchemaNode.AI.Agents;
using SchemaNode.AI.Workflow;
using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
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
/// Focused tests for the AI workflow node's provider abstraction and validation retry loop.
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
        MockAgentProvider provider = new("{\"min\":\"x\",\"max\":2}", "{\"min\":1,\"max\":2}");

        AIWorkflowRunResult result = await AIWorkflowRunner.RunAsync(Context, new AIWorkflowRunRequest
        {
            TargetType = target,
            Provider = provider,
            MaxRetries = 1,
            Instruction = "Return a minmax object.",
        });

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(2, result.CallCount);
        Assert.IsNotNull(result.Attempts[0].Error);
        StringAssert.Contains(provider.Requests[1].UserPrompt, "rejected");
        StringAssert.Contains(provider.Requests[1].UserPrompt, result.Attempts[0].Error!);
        StringAssert.Contains(provider.Requests[1].UserPrompt, "\"min\":\"x\"");
    }

    [TestMethod]
    public async Task RetryLoop_ExhaustionCarriesRealLastError()
    {
        ValueType target = (await Context.GetNodeTypeAsync<ValueType>("test.aiminmax"))!;
        MockAgentProvider provider = new("{\"min\":\"x\",\"max\":2}");

        AIWorkflowRunResult result = await AIWorkflowRunner.RunAsync(Context, new AIWorkflowRunRequest
        {
            TargetType = target,
            Provider = provider,
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
    public async Task MockProvider_RepeatsLastResponseDeterministically()
    {
        MockAgentProvider provider = new("first", "second");
        AIAgentRequest request = new() { SystemPrompt = "system", UserPrompt = "user" };

        Assert.AreEqual("first", await provider.CompleteAsync(request));
        Assert.AreEqual("second", await provider.CompleteAsync(request));
        Assert.AreEqual("second", await provider.CompleteAsync(request));
        Assert.AreEqual(3, provider.Calls);
    }

    [TestMethod]
    public void ProviderFactory_SelectsMockDeterministically()
    {
        AIAgentProviderFactory factory = new(new AIAgentOptions { Provider = AIProviderKind.Mock });

        IAgentProvider provider = factory.Create();
        Assert.IsInstanceOfType(provider, typeof(MockAgentProvider));

        IAgentProvider ollama = factory.Create(AIProviderKind.Ollama, "http://localhost:11434", "qwen3:8b", 60);
        Assert.AreEqual("ollama", ollama.Name);
    }

    [TestMethod]
    public async Task OllamaProvider_ClosedPortFailsWithTypedErrorAndNoHang()
    {
        OllamaAgentProvider provider = new("http://127.0.0.1:1", "qwen3:8b", 2);
        Stopwatch watch = Stopwatch.StartNew();

        try
        {
            await provider.CompleteAsync(new AIAgentRequest { SystemPrompt = "system", UserPrompt = "user" });
            Assert.Fail("The provider should not succeed when Ollama is unreachable.");
        }
        catch (AIAgentException ex)
        {
            Assert.AreEqual("ollama", ex.Provider);
            Assert.IsTrue(
                ex.Message.Contains("not reachable", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase),
                $"The typed error should identify an unreachable or timed-out server: {ex.Message}");
        }
        finally
        {
            watch.Stop();
        }

        Assert.IsTrue(watch.Elapsed < TimeSpan.FromSeconds(10), $"The unreachable provider took {watch.Elapsed}.");
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
        OllamaAgentProvider provider = new("http://localhost:11434", "qwen3:8b", 180);
        Stopwatch watch = Stopwatch.StartNew();

        AIWorkflowRunResult result = await AIWorkflowRunner.RunAsync(Context, new AIWorkflowRunRequest
        {
            TargetType = target,
            Provider = provider,
            MaxRetries = 1,
            MaxTokens = 160,
            Temperature = 0.0,
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
}
