using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using SchemaNode.AI;
using SchemaNode.AI.Chat;
using SchemaNode.Schema.Validation;

namespace SchemaNode.UnitTest.AI;

/// <summary>
/// Opt-in live run against a local Ollama server. It is skipped unless
/// <c>SCHEMA_CHAT_LIVE=1</c>, so the default unit test run never contacts a model.
/// <para>
/// Configuration environment variables: <c>SCHEMA_CHAT_LIVE</c>, <c>SCHEMA_CHAT_MODEL</c>,
/// <c>SCHEMA_CHAT_ENDPOINT</c>, <c>SCHEMA_CHAT_TIMEOUT_SECONDS</c>, <c>SCHEMA_CHAT_MAX_TOKENS</c>.
/// </para>
/// </summary>
[TestClass]
public class AIChatLiveTest : Base.AppTestBase
{
    private static bool LiveEnabled => Environment.GetEnvironmentVariable("SCHEMA_CHAT_LIVE") == "1";

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);

        services.AddSchemaChat(options =>
        {
            options.Provider = LiveEnabled ? SchemaChatProvider.Ollama : SchemaChatProvider.Mock;
            options.ModelId = Environment.GetEnvironmentVariable("SCHEMA_CHAT_MODEL") ?? "qwen3:8b";
            options.Endpoint = Environment.GetEnvironmentVariable("SCHEMA_CHAT_ENDPOINT") ?? "http://localhost:11434";
            options.MaxIterationsPerRequest = 10;
            options.RequestTimeoutSeconds = int.TryParse(
                Environment.GetEnvironmentVariable("SCHEMA_CHAT_TIMEOUT_SECONDS"), out int timeout) ? timeout : 300;
            options.MaxOutputTokens = int.TryParse(
                Environment.GetEnvironmentVariable("SCHEMA_CHAT_MAX_TOKENS"), out int maxTokens) ? maxTokens : 2048;
            options.DisableThinking = true;
        });
    }

    [TestMethod]
    public async Task Live_Ollama_ProducesMinMaxWithLowLimitRelation()
    {
        if (!LiveEnabled)
        {
            Assert.Inconclusive(
                "Live model run is opt-in. Set SCHEMA_CHAT_LIVE=1 (with a running Ollama server) to execute it.");
            return;
        }

        JsonNode current = JsonNode.Parse("""
        {
          "namespace": "",
          "name": "",
          "kind": "struct",
          "display": "MinMax",
          "struct": { "fields": [] }
        }
        """)!;

        Console.WriteLine("=== CURRENT EDITOR CONTENT SENT AS TARGET ===");
        Console.WriteLine(current.ToJsonString());
        Console.WriteLine();

        const string userMessage =
            "Create a struct named minmax with two integer fields: min and max. " +
            "Make max carry the lower limit of min, so max can never be lower than min. " +
            "Use English display labels. Keep the namespace exactly as it is.";

        using IServiceScope scope = Provider.CreateScope();
        IAIChat chat = scope.ServiceProvider.GetRequiredService<IAIChat>();

        StringBuilder streamedText = new();
        AIChatResult? result = null;
        Stopwatch stopwatch = Stopwatch.StartNew();

        await foreach (AIChatUpdate update in chat.ChatAsync(new AIChatRequest
        {
            UserContent = userMessage,
            Target = new AIChatTarget { Schema = current, IsNew = true },
            Options = new AIChatGenerationOptions(),
            Stream = true,
        }))
        {
            switch (update)
            {
                case AIChatStarted started:
                    Console.WriteLine($"=== STARTED conversation={started.ConversationId} ===");
                    break;
                case AIChatDelta delta:
                    streamedText.Append(delta.Text);
                    break;
            }
            if (update is AIChatCompleted completed)
                result = completed.Result;
            if (update is AIChatFailed failed)
                Console.WriteLine($"=== FAILED {failed.Code}: {failed.Message} ===");
        }

        stopwatch.Stop();

        Console.WriteLine();
        Console.WriteLine("=== RAW ASSISTANT TEXT (STREAMED) ===");
        Console.WriteLine(streamedText.ToString());
        Console.WriteLine();
        Console.WriteLine("=== FINAL RESULT ===");
        Console.WriteLine($"kind={result?.Kind}");
        Console.WriteLine($"message={result?.Message}");
        Console.WriteLine($"toolCalls={string.Join(", ", result?.ToolCalls ?? [])}");
        Console.WriteLine($"errorCode={result?.ErrorCode}");
        foreach (SchemaValidationError error in result?.Errors ?? [])
            Console.WriteLine($"error: {error.Path}: {error.Code} - {error.Message}");
        Console.WriteLine("=== FINAL SCHEMA (MODEL OUTPUT) ===");
        Console.WriteLine(result?.Schema?.ToJsonString() ?? "(none)");

        SchemaChatSession session = scope.ServiceProvider.GetRequiredService<SchemaChatSession>();
        Console.WriteLine("=== TOOL ERRORS ===");
        foreach (string error in session.ToolErrors)
            Console.WriteLine(error);
        Console.WriteLine($"proposal={session.Proposal?.Summary ?? "(none)"}");

        Console.WriteLine($"=== ELAPSED {stopwatch.Elapsed.TotalSeconds:F1}s ===");

        Assert.IsNotNull(result, "The live run did not produce a completed result.");
        Assert.AreEqual(AIChatResultKind.SchemaChange, result!.Kind,
            $"The live model did not produce a valid schema change. Message: {result.Message} " +
            $"Errors: {string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Message}"))}");
        Assert.IsTrue(result.Change?.IsValid, "The server side validator rejected the candidate.");

        JsonNode schema = result.Schema!;
        Assert.AreEqual("", schema["namespace"]?.GetValue<string>(),
            "The model changed the editor namespace.");
        Assert.AreEqual("struct", schema["kind"]?.GetValue<string>());

        JsonArray? fields = schema["struct"]?["fields"] as JsonArray;
        Assert.IsNotNull(fields);
        string[] fieldNames = fields!.Select(field => field?["name"]?.GetValue<string>() ?? "").ToArray();
        CollectionAssert.Contains(fieldNames, "min");
        CollectionAssert.Contains(fieldNames, "max");

        JsonArray? relations = schema["struct"]?["relations"] as JsonArray;
        Assert.IsNotNull(relations, "The model output does not contain any relation.");
        JsonNode? relation = relations!.FirstOrDefault(item =>
            item?["target"]?.GetValue<string>() == "max" &&
            item?["property"]?.GetValue<string>() == "lowlimit");
        Assert.IsNotNull(relation, "No relation targets max.lowlimit.");
        Assert.AreEqual("system.intrinsic.assign<system.int>",
            relation!["call"]?["func"]?.GetValue<string>());
        Assert.AreEqual("min",
            relation!["call"]?["args"]?[0]?["source"]?.GetValue<string>());
    }
}
