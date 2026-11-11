using System.Text.Json;
using System.Text.Json.Nodes;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Schema;
using SchemaNode.Struct;

namespace SchemaNode.UnitTest.App;

/// <summary>
/// Minimal verification for the workflow clone used by the Man workflow editor: appended
/// properties (display/description) must survive the save round-trip while runtime-only state
/// (error/usedBy/compatibles/provider) must not travel into the edit copy.
/// </summary>
[TestClass]
public class AppWorkflowSchemaRoundTripTest
{
    private static readonly string[] RuntimeKeys = ["error", "usedBy", "compatibles", "provider"];

    [TestMethod]
    public void CloneRoundTrip_KeepsAppendedPropertiesAndDropsRuntimeState()
    {
        AppWorkflowSchema source = new()
        {
            App = "demo",
            Name = "flow",
            Seqno = 5,
            Active = true,
            Nodes = [new AppWorkflowNodeSchema { Name = "start", Type = "app/start" }],
        };
        source.SetProperty<Display, LocaleString>("flow display");
        source.SetProperty<Description, LocaleString>("flow description");
        source.Nodes[0].SetProperty<Display, LocaleString>("node display");
        source.Nodes[0].Error = "runtime error";
        source.Extensions!["usedBy"] = new JsonArray("other");
        source.Extensions!["provider"] = "runtime";

        // Same steps as WorkflowConfigViewModel.Clone: serialize through the schema options,
        // strip runtime keys from the workflow and each node, then deserialize and restore the
        // ignored Seqno.
        JsonSerializerOptions options = SchemaNodeJsonExtension.GetSerializerOptions();
        JsonObject json = (JsonObject)JsonSerializer.SerializeToNode(source, options)!;
        RemoveRuntimeKeys(json);
        foreach (JsonNode? node in json["nodes"] as JsonArray ?? new JsonArray())
            if (node is JsonObject nodeObject)
                RemoveRuntimeKeys(nodeObject);

        AppWorkflowSchema copy = JsonSerializer.Deserialize<AppWorkflowSchema>(json, options)!;
        copy.Seqno = source.Seqno;

        Assert.AreEqual("flow display", copy.GetProperty<Display>()?.Value?.Key, "display must survive the clone");
        Assert.AreEqual("flow description", copy.GetProperty<Description>()?.Value?.Key, "description must survive the clone");
        Assert.AreEqual("node display", copy.Nodes[0].GetProperty<Display>()?.Value?.Key, "node display must survive the clone");
        Assert.IsNull(copy.Nodes[0].Error, "runtime error state must not travel into the edit copy");
        Assert.IsFalse(copy.Extensions?.ContainsKey("usedBy") ?? false, "usedBy must not travel into the edit copy");
        Assert.IsFalse(copy.Extensions?.ContainsKey("provider") ?? false, "provider must not travel into the edit copy");
        Assert.AreEqual(5, copy.Seqno, "Seqno is JsonIgnore and must be restored explicitly");
    }

    private static void RemoveRuntimeKeys(JsonObject target)
    {
        foreach (string key in target.Select(pair => pair.Key)
                     .Where(key => RuntimeKeys.Any(runtime => runtime.Equals(key, StringComparison.OrdinalIgnoreCase)))
                     .ToArray())
        {
            target.Remove(key);
        }
    }
}
