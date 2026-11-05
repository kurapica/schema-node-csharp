using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using SchemaNode.AI;
using SchemaNode.AI.Chat;
using SchemaNode.Schema;
using SchemaNode.Schema.Validation;

namespace SchemaNode.UnitTest.AI;

/// <summary>
/// Tests for the transport agnostic <see cref="IAIChat"/> contract, its local implementation and
/// the HTTP implementation. No live model is contacted.
/// </summary>
[TestClass]
public class AIChatContractTest : Base.AppTestBase
{
    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);

        services.AddSchemaChatCore(options =>
        {
            options.Provider = SchemaChatProvider.Mock;
            options.ModelId = "mock";
            options.MaxIterationsPerRequest = 6;
            options.RequestTimeoutSeconds = 30;
        });
    }

    private static async Task<AIChatResult> RunAsync(IAIChat chat, AIChatRequest request)
    {
        AIChatResult? result = null;
        await foreach (AIChatUpdate update in chat.ChatAsync(request))
        {
            if (update is AIChatCompleted completed)
                result = completed.Result;
        }

        Assert.IsNotNull(result, "The chat client did not produce a completed result.");
        return result!;
    }

    [TestMethod]
    public async Task LocalChat_MockProvider_ReturnsCompleteMinMaxSchema()
    {
        using IServiceScope scope = Provider.CreateScope();
        IAIChat chat = scope.ServiceProvider.GetRequiredService<IAIChat>();

        AIChatResult result = await RunAsync(chat, new AIChatRequest
        {
            UserContent = "Create a MinMax struct with min and max integers, and tie max lowlimit to min.",
            Target = new AIChatTarget
            {
                Schema = JsonNode.Parse("""{"namespace":"","name":"","kind":"struct"}"""),
                IsNew = true,
            },
        });

        Assert.AreEqual(AIChatResultKind.SchemaChange, result.Kind, result.Message);
        Assert.IsEmpty(result.Errors);
        Assert.IsNotNull(result.Schema);

        JsonNode schema = result.Schema!;
        Assert.AreEqual("minmax", schema["name"]?.GetValue<string>());
        Assert.AreEqual("struct", schema["kind"]?.GetValue<string>());

        JsonArray? fields = schema["struct"]?["fields"] as JsonArray;
        Assert.IsNotNull(fields);
        Assert.AreEqual(2, fields!.Count);
        CollectionAssert.AreEqual(new[] { "min", "max" },
            fields.Select(field => field?["name"]?.GetValue<string>()).ToArray());

        JsonArray? relations = schema["struct"]?["relations"] as JsonArray;
        Assert.IsNotNull(relations, "The mock proposal must cover relations as well as fields.");
        Assert.AreEqual(1, relations!.Count);
        Assert.AreEqual("max", relations[0]?["target"]?.GetValue<string>());
        Assert.AreEqual("lowlimit", relations[0]?["property"]?.GetValue<string>());
        Assert.AreEqual("system.intrinsic.assign<system.int>",
            relations[0]?["call"]?["func"]?.GetValue<string>());

        Assert.IsNotNull(result.Change);
        Assert.IsTrue(result.Change!.IsValid);
        Assert.AreEqual(SchemaCandidateValidator.OperationCreate, result.Change.Operation);
    }

    [TestMethod]
    public async Task LocalChat_MockProvider_PlainQuestion_ReturnsChat()
    {
        using IServiceScope scope = Provider.CreateScope();
        IAIChat chat = scope.ServiceProvider.GetRequiredService<IAIChat>();

        AIChatResult result = await RunAsync(chat, new AIChatRequest
        {
            UserContent = "What is a struct?",
        });

        Assert.AreEqual(AIChatResultKind.Chat, result.Kind, result.Message);
        Assert.IsNull(result.Schema);
        Assert.IsNotEmpty(result.Message!);
    }

    [TestMethod]
    public async Task HttpChat_MapsWireResponse_ToContractResult()
    {
        ChatSchemaResponse wire = new()
        {
            ConversationId = "conv-1",
            Kind = SchemaChatResultKind.SchemaChange,
            Message = "Done.",
            ToolCalls = ["propose_schema_change"],
            Change = new SchemaChangeCandidate
            {
                Operation = SchemaCandidateValidator.OperationCreate,
                TargetName = "demo.address",
                IsValid = true,
                Changes = ["+struct.fields[street]"],
                Normalized = JsonNode.Parse("""
                {
                  "namespace": "demo",
                  "name": "address",
                  "kind": "struct",
                  "struct": { "fields": [ { "name": "street", "type": "system.string" } ] }
                }
                """),
            },
        };

        string payload = JsonSerializer.Serialize(wire, SchemaNodeJsonExtension.GetSerializerOptions());
        StubHandler handler = new(payload);
        using HttpClient client = new(handler);
        HttpAIChat chat = new(client, new AIChatWebApiOptions
        {
            BaseAddress = "http://localhost/schema",
        });

        AIChatResult result = await RunAsync(chat, new AIChatRequest
        {
            UserContent = "Create an address.",
            ConversationId = "conv-1",
        });

        Assert.AreEqual(AIChatResultKind.SchemaChange, result.Kind);
        Assert.AreEqual("conv-1", result.ConversationId);
        Assert.AreEqual("Done.", result.Message);
        Assert.IsNotNull(result.Schema);
        Assert.AreEqual("address", result.Schema!["name"]?.GetValue<string>());
        CollectionAssert.AreEqual(new[] { "propose_schema_change" }, result.ToolCalls);
        CollectionAssert.AreEqual(new[] { "+struct.fields[street]" }, result.Changes);
        Assert.AreEqual(1, handler.RequestCount);
    }

    [TestMethod]
    public async Task HttpChat_ServerFailure_ReturnsErrorResult()
    {
        StubHandler handler = new("""{"message":"The assistant is unavailable."}""",
            HttpStatusCode.InternalServerError);
        using HttpClient client = new(handler);
        HttpAIChat chat = new(client, new AIChatWebApiOptions
        {
            BaseAddress = "http://localhost/schema",
        });

        AIChatResult result = await RunAsync(chat, new AIChatRequest { UserContent = "hello" });

        Assert.AreEqual(AIChatResultKind.Error, result.Kind);
        Assert.AreEqual("http_error", result.ErrorCode);
        StringAssert.Contains(result.Message, "unavailable");
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _payload;
        private readonly HttpStatusCode _statusCode;

        public StubHandler(string payload, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _payload = payload;
            _statusCode = statusCode;
        }

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_payload, Encoding.UTF8, "application/json"),
            });
        }
    }
}
