using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using SchemaNode.AI;
using SchemaNode.AI.Chat;
using SchemaNode.Context;
using SchemaNode.Schema;
using SchemaNode.Schema.Provider;
using SchemaNode.Schema.Validation;
using SchemaNode.Service;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.UnitTest.AI;

/// <summary>
/// End to end tests for the schema assistant protocol, tool loop, validation capture and
/// conversation state. A scripted <see cref="IChatClient"/> replaces the real model.
/// </summary>
[TestClass]
public class SchemaChatServiceTest : Base.AppTestBase
{
    private FakeChatClient _client = null!;

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);

        _client = new FakeChatClient();
        services.AddSingleton<IChatClient>(_client);
        services.AddSchemaChat(options =>
        {
            options.Provider = SchemaChatProvider.Ollama;
            options.ModelId = "fake-model";
            options.MaxIterationsPerRequest = 6;
            options.RequestTimeoutSeconds = 30;
        });
    }

    private (SchemaChatService Service, IServiceProvider Scope) CreateService()
    {
        IServiceScope scope = Provider.CreateScope();
        return (scope.ServiceProvider.GetRequiredService<SchemaChatService>(), scope.ServiceProvider);
    }

    private static async Task<ChatSchemaResponse> RunAsync(SchemaChatService service, ChatSchemaRequest request)
    {
        await foreach (SchemaChatUpdate update in service.ExecuteAsync(request, "tester"))
        {
            if (update is SchemaChatCompleted completed)
                return completed.Response;
        }

        Assert.Fail("The service did not produce a completed response.");
        return null!;
    }

    private static JsonElement Element(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [TestMethod]
    public async Task PlainChat_ReturnsChatResult_WithoutChange()
    {
        _client.SetText("A struct groups named fields with value types.");

        var (service, _) = CreateService();
        ChatSchemaResponse response = await RunAsync(service, new ChatSchemaRequest
        {
            Message = "What is a struct?",
        });

        Assert.AreEqual(SchemaChatResultKind.Chat, response.Kind);
        Assert.IsNull(response.Change);
        StringAssert.Contains(response.Message, "groups named fields");
        Assert.IsFalse(string.IsNullOrWhiteSpace(response.ConversationId));
        Assert.AreEqual(0, (response.ToolCalls ?? []).Length);
    }

    [TestMethod]
    public async Task ValidProposal_ReturnsSchemaChange_AndNeverPersists()
    {
        const string candidate = """
        {
          "namespace": "",
          "name": "ai_chat_address",
          "kind": "struct",
          "display": "AI Chat Address",
          "struct": { "fields": [ { "name": "street", "type": "system.string" } ] }
        }
        """;

        _client.Script(
            (_, _) => FakeChatClient.ToolCall("propose_schema_change", new Dictionary<string, object?>
            {
                ["schema"] = Element(candidate),
                ["summary"] = "Created a minimal address struct.",
            }),
            (_, _) => FakeChatClient.Text("I created the address schema."));

        var (service, _) = CreateService();
        ChatSchemaResponse response = await RunAsync(service, new ChatSchemaRequest
        {
            Message = "Create an address struct with a street field.",
            CurrentSchema = JsonNode.Parse("""{"namespace":"","name":"","kind":"struct"}"""),
            IsNew = true,
        });

        Assert.AreEqual(SchemaChatResultKind.SchemaChange, response.Kind, response.Message);
        Assert.IsNotNull(response.Change);
        Assert.IsTrue(response.Change!.IsValid);
        Assert.AreEqual("ai_chat_address", response.Change.TargetName);
        Assert.AreEqual(SchemaCandidateValidator.OperationCreate, response.Change.Operation);
        Assert.IsNotNull(response.Change.Schema);
        Assert.IsNotNull(response.Change.Normalized);
        Assert.IsNotNull(response.Change.Normalized!["struct"]);
        Assert.IsNotEmpty(response.Change.Changes);
        CollectionAssert.Contains(response.ToolCalls!, "propose_schema_change");

        Assert.IsNull(await Context.GetNodeTypeAsync("ai_chat_address"),
            "The assistant must not persist a candidate without an explicit user save.");
    }

    [TestMethod]
    public async Task ProposalWithoutSummary_StillSucceeds()
    {
        const string candidate = """
        {
          "namespace": "",
          "name": "ai_chat_no_summary",
          "kind": "struct",
          "struct": { "fields": [ { "name": "value", "type": "system.string" } ] }
        }
        """;

        // qwen3 sometimes omits the optional summary argument. The terminal tool must still work.
        _client.Script(
            (_, _) => FakeChatClient.ToolCall("propose_schema_change", new Dictionary<string, object?>
            {
                ["schema"] = Element(candidate),
            }),
            (_, _) => FakeChatClient.Text("Created."));

        var (service, _) = CreateService();
        ChatSchemaResponse response = await RunAsync(service, new ChatSchemaRequest
        {
            Message = "Create a struct.",
            IsNew = true,
        });

        Assert.AreEqual(SchemaChatResultKind.SchemaChange, response.Kind, response.Message);
        Assert.IsNotNull(response.Change);
        Assert.IsTrue(response.Change!.IsValid);
        Assert.IsFalse(string.IsNullOrWhiteSpace(response.Change.Summary));
    }

    [TestMethod]
    public async Task InvalidProposal_ReturnsValidationFailed_AndKeepsExistingSchema()
    {
        NodeSchema existing = new()
        {
            Namespace = "",
            Name = "ai_chat_customer",
            Kind = NODE_KIND_STRUCT,
        };
        existing.SetProperty<StructProperty, StructSchema>(new StructSchema
        {
            Fields =
            [
                new StructFieldSchema { Name = "name", Type = NS_SYSTEM_STRING },
                new StructFieldSchema { Name = "age", Type = NS_SYSTEM_INT },
            ],
        });
        Assert.IsTrue(await Context.SaveSchemaAsync(existing));

        const string candidate = """
        {
          "namespace": "",
          "name": "ai_chat_customer",
          "kind": "struct",
          "struct": { "fields": [
            { "name": "name", "type": "system.string" },
            { "name": "age", "type": "system.not_a_type" }
          ] }
        }
        """;

        _client.Script(
            (_, _) => FakeChatClient.ToolCall("propose_schema_change", new Dictionary<string, object?>
            {
                ["schema"] = Element(candidate),
                ["summary"] = "Changed the age type.",
            }),
            (_, _) => FakeChatClient.Text("The candidate is invalid."));

        var (service, _) = CreateService();
        ChatSchemaResponse response = await RunAsync(service, new ChatSchemaRequest
        {
            Message = "Change age to a type that does not exist.",
            Target = "ai_chat_customer",
            CurrentSchema = JsonNode.Parse("""
            {
              "namespace": "", "name": "ai_chat_customer", "kind": "struct",
              "struct": { "fields": [
                { "name": "name", "type": "system.string" },
                { "name": "age", "type": "system.int" }
              ] }
            }
            """),
        });

        Assert.AreEqual(SchemaChatResultKind.ValidationFailed, response.Kind, response.Message);
        Assert.IsNotNull(response.Change);
        Assert.IsFalse(response.Change!.IsValid);
        Assert.IsTrue(response.Change.Errors.Any(e => e.Code == SchemaValidationErrorCodes.UnknownType));

        var loaded = (await Context.GetNodeTypeAsync("ai_chat_customer"))!.GetNodeSchema();
        var fields = loaded.GetProperty<StructProperty>()!.Value!.Fields;
        Assert.AreEqual(2, fields.Length);
        Assert.AreEqual(NS_SYSTEM_INT, fields[1].Type);
    }

    [TestMethod]
    public async Task McpGetSchemaTool_IsInvokable_ThroughTheAgent()
    {
        _client.Script(
            (_, _) => FakeChatClient.ToolCall("get_schema", new Dictionary<string, object?>
            {
                ["name"] = "system.schema.node.schema",
            }),
            (_, _) => FakeChatClient.Text("The node schema meta type has namespace, name and kind fields."));

        var (service, _) = CreateService();
        ChatSchemaResponse response = await RunAsync(service, new ChatSchemaRequest
        {
            Message = "What fields define a node schema?",
        });

        Assert.AreEqual(SchemaChatResultKind.Chat, response.Kind, response.Message);
        CollectionAssert.Contains(response.ToolCalls!, "get_schema");
        StringAssert.Contains(response.Message, "namespace");
        Assert.IsNull(response.Change);
    }

    [TestMethod]
    public async Task NewSchemaInNamespace_DoesNotBindTheTargetToTheNamespace()
    {
        NodeSchema ns = new()
        {
            Namespace = "",
            Name = "ai_chat_ns",
            Kind = NODE_KIND_NAMESPACE,
        };
        Assert.IsTrue(await Context.SaveSchemaAsync(ns));

        const string candidate = """
        {
          "namespace": "ai_chat_ns",
          "name": "ai_chat_new",
          "kind": "struct",
          "struct": { "fields": [ { "name": "value", "type": "system.string" } ] }
        }
        """;

        _client.Script(
            (_, _) => FakeChatClient.ToolCall("propose_schema_change", new Dictionary<string, object?>
            {
                ["schema"] = Element(candidate),
                ["summary"] = "Created a new struct in the namespace.",
            }),
            (_, _) => FakeChatClient.Text("Created."));

        var (service, _) = CreateService();
        ChatSchemaResponse response = await RunAsync(service, new ChatSchemaRequest
        {
            Message = "Create a struct in this namespace.",
            CurrentSchema = JsonNode.Parse("""{"namespace":"ai_chat_ns","name":"","kind":"struct"}"""),
            IsNew = true,
        });

        Assert.AreEqual(SchemaChatResultKind.SchemaChange, response.Kind, response.Message);
        Assert.AreEqual("ai_chat_ns.ai_chat_new", response.Change!.TargetName);
        Assert.AreEqual(SchemaCandidateValidator.OperationCreate, response.Change.Operation);
    }

    [TestMethod]
    public async Task ToolError_WithoutFinalText_IsReportedAsError()
    {
        _client.Script(
            (_, _) => FakeChatClient.ToolCall("propose_schema_change", new Dictionary<string, object?>
            {
                ["summary"] = "Missing schema argument.",
            }),
            (_, _) => FakeChatClient.Text(string.Empty, append: false));

        var (service, _) = CreateService();
        ChatSchemaResponse response = await RunAsync(service, new ChatSchemaRequest
        {
            Message = "Propose a schema.",
        });

        Assert.AreEqual(SchemaChatResultKind.Error, response.Kind);
        Assert.AreEqual("tool_error", response.ErrorCode);
        Assert.IsNull(response.Change);
    }

    [TestMethod]
    public async Task ConversationHistory_IsReusedAcrossRequests()
    {
        _client.SetText("first answer");

        var (service1, _) = CreateService();
        ChatSchemaResponse first = await RunAsync(service1, new ChatSchemaRequest
        {
            Message = "first question",
        });

        _client.SetText("second answer");
        _client.CaptureRequests = true;

        var (service2, _) = CreateService();
        ChatSchemaResponse second = await RunAsync(service2, new ChatSchemaRequest
        {
            Message = "second question",
            ConversationId = first.ConversationId,
        });

        Assert.AreEqual(first.ConversationId, second.ConversationId);
        Assert.IsTrue(_client.Requests.Count > 0);
        List<ChatMessage> messages = _client.Requests[^1].ToList();
        Assert.IsTrue(messages.Any(m => m.Role == ChatRole.User && m.Text == "first question"));
        Assert.IsTrue(messages.Any(m => m.Role == ChatRole.Assistant && m.Text == "first answer"));
        Assert.IsTrue(messages.Any(m => m.Role == ChatRole.User && m.Text == "second question"));
    }

    [TestMethod]
    public async Task Streaming_EmitsTextDeltas_AndFinalResponse()
    {
        _client.SetStreamingText(["Hello ", "from ", "the assistant."]);

        var (service, _) = CreateService();
        List<SchemaChatUpdate> updates = [];
        await foreach (SchemaChatUpdate update in service.ExecuteStreamAsync(new ChatSchemaRequest
        {
            Message = "Say hello.",
        }, "tester"))
        {
            updates.Add(update);
        }

        Assert.IsTrue(updates[0] is SchemaChatStarted);
        string text = string.Concat(updates.OfType<SchemaChatTextDelta>().Select(d => d.Text));
        StringAssert.Contains(text, "Hello from the assistant.");
        ChatSchemaResponse response = updates.OfType<SchemaChatCompleted>().Single().Response;
        Assert.AreEqual(SchemaChatResultKind.Chat, response.Kind);
        StringAssert.Contains(response.Message, "Hello from the assistant.");
    }

    [TestMethod]
    public async Task CurrentSchemaPromptOrder_IsCanonicalized()
    {
        _client.SetText("ok");
        _client.CaptureRequests = true;

        var (service1, _) = CreateService();
        await RunAsync(service1, new ChatSchemaRequest
        {
            Message = "Explain the current schema.",
            CurrentSchema = JsonNode.Parse("""
            {
              "namespace": "",
              "name": "ai_order",
              "kind": "struct",
              "display": "Order",
              "struct": { "fields": [ { "name": "id", "type": "system.string" } ] }
            }
            """),
        });
        string firstPrompt = _client.Requests[0].First(m => m.Role == ChatRole.System).Text ?? "";

        _client.SetText("ok");
        var (service2, _) = CreateService();
        await RunAsync(service2, new ChatSchemaRequest
        {
            Message = "Explain the current schema.",
            CurrentSchema = JsonNode.Parse("""
            {
              "struct": { "fields": [ { "type": "system.string", "name": "id" } ] },
              "display": "Order",
              "kind": "struct",
              "name": "ai_order",
              "namespace": ""
            }
            """),
        });
        string secondPrompt = _client.Requests[^1].First(m => m.Role == ChatRole.System).Text ?? "";

        Assert.AreEqual(firstPrompt, secondPrompt);
    }

    [TestMethod]
    public async Task IdenticalProposal_IsRejectedAsNoChange()
    {
        const string current = """
        {
          "namespace": "",
          "name": "ai_no_change",
          "kind": "struct",
          "display": { "key": "整数范围", "trans": [] },
          "struct": { "fields": [ { "name": "min", "type": "system.int", "display": { "key": "最小值", "trans": [] } } ] }
        }
        """;

        _client.Script(
            (_, _) => FakeChatClient.ToolCall("propose_schema_change", new Dictionary<string, object?>
            {
                ["schema"] = Element(current),
                ["summary"] = "Updated displays.",
            }),
            (_, _) => FakeChatClient.Text("Updated displays."));

        var (service, _) = CreateService();
        ChatSchemaResponse response = await RunAsync(service, new ChatSchemaRequest
        {
            Message = "调整字段",
            CurrentSchema = JsonNode.Parse(current),
        });

        Assert.AreEqual(SchemaChatResultKind.ValidationFailed, response.Kind, response.Message);
        Assert.IsNotNull(response.Change);
        Assert.IsTrue(response.Change!.Errors.Any(e => e.Code == SchemaValidationErrorCodes.NoChange));
    }

    [TestMethod]
    public async Task EmptyToolLoop_UsesDirectJsonFallback()
    {
        _client.Script(
            (_, _) => FakeChatClient.Text(string.Empty, append: false),
            (_, _) => FakeChatClient.Text(string.Empty, append: false),
            (_, _) => FakeChatClient.Text("""
            {
              "namespace": "",
              "name": "ai_fallback",
              "kind": "struct",
              "struct": { "fields": [
                { "name": "value", "type": "system.string" },
                { "name": "email", "type": "system.string" }
              ] }
            }
            """));

        var (service, _) = CreateService();
        ChatSchemaResponse response = await RunAsync(service, new ChatSchemaRequest
        {
            Message = "增加 email 字段，保留 value。",
            CurrentSchema = JsonNode.Parse("""
            {
              "namespace": "", "name": "ai_fallback", "kind": "struct",
              "struct": { "fields": [ { "name": "value", "type": "system.string" } ] }
            }
            """),
        });

        Assert.AreEqual(SchemaChatResultKind.SchemaChange, response.Kind, response.Message);
        Assert.IsNotNull(response.Change);
        Assert.IsTrue(response.Change!.IsValid);
        Assert.IsTrue(response.Change.Normalized!["struct"]!["fields"]!.AsArray().Any(
            field => field!["name"]!.GetValue<string>() == "email"));
    }

    [TestMethod]
    public async Task LocalizationRequest_UsesDeterministicDisplayFallback()
    {
        _client.Script(
            (_, _) => FakeChatClient.Text(string.Empty, append: false),
            (_, _) => FakeChatClient.Text(string.Empty, append: false),
            (_, _) => FakeChatClient.Text(string.Empty, append: false));

        const string current = """
        {
          "namespace": "",
          "name": "IntegerRange",
          "kind": "struct",
          "display": { "key": "整数范围", "trans": [] },
          "struct": {
            "fields": [
              { "name": "min", "type": "system.int", "display": { "key": "最小值", "trans": [] } },
              { "name": "max", "type": "system.int", "display": { "key": "最大值", "trans": [] } }
            ]
          }
        }
        """;

        var (service, _) = CreateService();
        ChatSchemaResponse response = await RunAsync(service, new ChatSchemaRequest
        {
            Message = "字段的display，英文做默认，补充中文(zhCN)做翻译",
            CurrentSchema = JsonNode.Parse(current),
        });

        Assert.AreEqual(SchemaChatResultKind.SchemaChange, response.Kind, response.Message);
        var normalized = response.Change!.Normalized!;
        Assert.AreEqual("Integer Range", normalized["display"]!["key"]!.GetValue<string>());
        Assert.AreEqual("整数范围", normalized["display"]!["trans"]!.AsArray()[0]!["tran"]!.GetValue<string>());

        var fields = normalized["struct"]!["fields"]!.AsArray();
        Assert.AreEqual("Minimum", fields[0]!["display"]!["key"]!.GetValue<string>());
        Assert.AreEqual("最小值", fields[0]!["display"]!["trans"]!.AsArray()[0]!["tran"]!.GetValue<string>());
        Assert.AreEqual("Maximum", fields[1]!["display"]!["key"]!.GetValue<string>());
    }

    [TestMethod]
    public async Task EmptyTurn_RetriesAndForcesProposal()
    {
        const string candidate = """
        {
          "namespace": "",
          "name": "ai_retry_target",
          "kind": "struct",
          "struct": { "fields": [
            { "name": "value", "type": "system.string" },
            { "name": "email", "type": "system.string" }
          ] }
        }
        """;

        _client.Script(
            (_, _) => FakeChatClient.Text(string.Empty, append: false),
            (_, _) => FakeChatClient.ToolCall("propose_schema_change", new Dictionary<string, object?>
            {
                ["schema"] = Element(candidate),
                ["summary"] = "Added email.",
            }),
            (_, _) => FakeChatClient.Text("Added the email field."));

        var (service, _) = CreateService();
        ChatSchemaResponse response = await RunAsync(service, new ChatSchemaRequest
        {
            Message = "增加 email 字段，保留 value。",
            CurrentSchema = JsonNode.Parse("""
            {
              "namespace": "", "name": "ai_retry_target", "kind": "struct",
              "struct": { "fields": [ { "name": "value", "type": "system.string" } ] }
            }
            """),
        });

        Assert.AreEqual(SchemaChatResultKind.SchemaChange, response.Kind, response.Message);
        Assert.IsNotNull(response.Change);
        Assert.IsTrue(response.Change!.IsValid);
        Assert.IsTrue(_client.CallCount >= 3);
    }

    [TestMethod]
    public async Task StreamingEmptyResponse_FallsBackToNonStreaming()
    {
        _client.SetStreamingThenText([], "Fallback answer");

        var (service, _) = CreateService();
        List<SchemaChatUpdate> updates = [];
        await foreach (SchemaChatUpdate update in service.ExecuteStreamAsync(new ChatSchemaRequest
        {
            Message = "Answer me.",
        }, "tester"))
        {
            updates.Add(update);
        }

        ChatSchemaResponse response = updates.OfType<SchemaChatCompleted>().Single().Response;
        Assert.AreEqual(SchemaChatResultKind.Chat, response.Kind, response.Message);
        Assert.AreEqual("Fallback answer", response.Message);
        Assert.IsTrue(_client.CallCount >= 2);
    }

    [TestMethod]
    public async Task CandidateMustKeepEditorTarget()
    {
        NodeSchema existing = new()
        {
            Namespace = "",
            Name = "ai_chat_target",
            Kind = NODE_KIND_STRUCT,
        };
        existing.SetProperty<StructProperty, StructSchema>(new StructSchema
        {
            Fields = [new StructFieldSchema { Name = "keep", Type = NS_SYSTEM_STRING }],
        });
        Assert.IsTrue(await Context.SaveSchemaAsync(existing));

        const string candidate = """
        {
          "namespace": "",
          "name": "ai_chat_other",
          "kind": "struct",
          "struct": { "fields": [ { "name": "keep", "type": "system.string" } ] }
        }
        """;

        _client.Script(
            (_, _) => FakeChatClient.ToolCall("propose_schema_change", new Dictionary<string, object?>
            {
                ["schema"] = Element(candidate),
                ["summary"] = "Renamed the schema.",
            }),
            (_, _) => FakeChatClient.Text("I could not apply that change."));

        var (service, _) = CreateService();
        ChatSchemaResponse response = await RunAsync(service, new ChatSchemaRequest
        {
            Message = "Rename it.",
            Target = "ai_chat_target",
            CurrentSchema = JsonNode.Parse("""
            {
              "namespace": "", "name": "ai_chat_target", "kind": "struct",
              "struct": { "fields": [ { "name": "keep", "type": "system.string" } ] }
            }
            """),
        });

        Assert.AreEqual(SchemaChatResultKind.ValidationFailed, response.Kind, response.Message);
        Assert.IsTrue(response.Change!.Errors.Any(e => e.Code == SchemaValidationErrorCodes.TargetNameMismatch));
    }

    [TestMethod]
    public async Task ContextTargetMismatch_IsRejectedBeforeTheModel()
    {
        var (service, _) = CreateService();
        ChatSchemaResponse response = await RunAsync(service, new ChatSchemaRequest
        {
            Message = "Change it.",
            Target = "one",
            CurrentSchema = JsonNode.Parse("""{"namespace":"","name":"two","kind":"struct","struct":{"fields":[]}}"""),
        });

        Assert.AreEqual(SchemaChatResultKind.Error, response.Kind);
        Assert.AreEqual("invalid_context", response.ErrorCode);
        Assert.AreEqual(0, _client.CallCount);
    }

    /// <summary>
    /// A scripted chat client. Each call consumes the next responder; a responder can either
    /// return a text answer or a tool call, letting the real FunctionInvokingChatClient loop run.
    /// </summary>
    private sealed class FakeChatClient : IChatClient
    {
        private readonly Queue<Func<int, IReadOnlyList<ChatMessage>, ChatResponse>> _responders = new();
        private string[]? _streamingText;

        public int CallCount { get; private set; }
        public bool CaptureRequests { get; set; }
        public List<IReadOnlyList<ChatMessage>> Requests { get; } = [];

        public void SetText(string text)
        {
            _responders.Clear();
            _streamingText = null;
            _responders.Enqueue((_, _) => TextResponse(text));
        }

        public void SetStreamingText(string[] parts)
        {
            _responders.Clear();
            _streamingText = parts;
        }

        public void SetStreamingThenText(string[] parts, string text)
        {
            _responders.Clear();
            _streamingText = parts;
            _responders.Enqueue((_, _) => TextResponse(text));
        }

        public void Script(params Func<int, IReadOnlyList<ChatMessage>, ChatResponse>[] responders)
        {
            _responders.Clear();
            _streamingText = null;
            foreach (var responder in responders)
                _responders.Enqueue(responder);
        }

        public static ChatResponse TextResponse(string text)
            => new(new ChatMessage(ChatRole.Assistant, text));

        public static ChatResponse Text(string text, bool append = true) => TextResponse(text);

        public static ChatResponse ToolCall(string name, IDictionary<string, object?> arguments)
            => new(new ChatMessage(ChatRole.Assistant,
            [
                new FunctionCallContent($"call-{Guid.NewGuid():N}", name, arguments),
            ]));

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            IReadOnlyList<ChatMessage> list = messages.ToList();
            if (CaptureRequests) Requests.Add(list);

            if (_responders.Count == 0)
                return Task.FromResult(TextResponse("fallback answer"));

            return Task.FromResult(_responders.Dequeue()(CallCount, list));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            CallCount++;
            IReadOnlyList<ChatMessage> list = messages.ToList();
            if (CaptureRequests) Requests.Add(list);

            string[] parts = _streamingText ?? ["fallback ", "answer"];
            foreach (string part in parts)
                yield return new ChatResponseUpdate(ChatRole.Assistant, part);
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
            => serviceType == typeof(IChatClient) ? this : null;

        public void Dispose()
        {
        }
    }
}
