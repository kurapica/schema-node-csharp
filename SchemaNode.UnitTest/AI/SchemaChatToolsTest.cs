using System.Text.Json.Nodes;
using SchemaNode.AI.Mcp;

namespace SchemaNode.UnitTest.AI;

/// <summary>
/// Tests for the read-only system discovery tools used by the schema assistant. No model is
/// contacted; the assertions read the runtime type system directly.
/// </summary>
[TestClass]
public class SchemaChatToolsTest : Base.AppTestBase
{
    [TestMethod]
    public async Task ListSchemas_SystemIntrinsic_ExposesFunctions()
    {
        JsonNode result = await SchemaTools.ListSchemas(Context, "system.intrinsic");

        JsonArray? schemas = result["schemas"] as JsonArray;
        Assert.IsNotNull(schemas, "The list must return a schemas array.");
        Assert.IsTrue(schemas!.Count > 0, "The system.intrinsic namespace must declare functions.");

        string[] names = schemas
            .Select(schema => schema?["name"]?.GetValue<string>() ?? string.Empty)
            .ToArray();

        CollectionAssert.Contains(names, "system.intrinsic.assign");
        CollectionAssert.Contains(names, "system.intrinsic.default");
    }

    [TestMethod]
    public async Task ListSchemas_IntProperties_ExposeLowLimit()
    {
        JsonNode result = await SchemaTools.ListSchemas(Context, "system.schema.prop.int");
        JsonArray? schemas = result["schemas"] as JsonArray;
        Assert.IsNotNull(schemas);

        string[] names = schemas!
            .Select(schema => schema?["name"]?.GetValue<string>() ?? string.Empty)
            .ToArray();

        CollectionAssert.Contains(names, "system.schema.prop.int.lowlimit");
        CollectionAssert.Contains(names, "system.schema.prop.int.uplimit");
    }

    [TestMethod]
    public async Task GetFunction_AssignInt_ReturnsResolvedSignature()
    {
        JsonNode result = await SchemaTools.GetFunction(Context, "system.intrinsic.assign<system.int>");

        Assert.AreEqual("system.intrinsic.assign<system.int>", result["name"]?.GetValue<string>());
        Assert.AreEqual("system.int", result["returns"]?.GetValue<string>());

        JsonArray? args = result["args"] as JsonArray;
        Assert.IsNotNull(args);
        Assert.AreEqual(1, args!.Count);
        Assert.AreEqual("value", args[0]?["name"]?.GetValue<string>());
        Assert.AreEqual("system.int", args[0]?["type"]?.GetValue<string>());
        Assert.IsNotNull(args[0]?["required"], "The signature must report whether the argument is required.");
    }
}
