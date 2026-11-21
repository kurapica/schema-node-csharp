using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using SchemaNode.AI;
using SchemaNode.AI.Chat;
using SchemaNode.Schema.Validation;

namespace SchemaNode.UnitTest.AI;

/// <summary>
/// Tests for the transport agnostic <see cref="IAIChat"/> contract and its local implementation.
/// No live model is contacted.
/// </summary>
[TestClass]
public class AIChatContractTest : Base.AppTestBase
{
    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);

        services.AddSchemaChat(options =>
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
}
