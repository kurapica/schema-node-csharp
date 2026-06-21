using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Api.Schema.Application;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.UnitTest;

/// <summary>
/// Tests for node JSON serialization: scalar, struct
/// </summary>
[TestClass]
public class NodeSerializationTest : TestBase
{
    /// <summary>
    /// ScalarTypeNode.ToJson() produces the correct value
    /// </summary>
    [TestMethod]
    public async Task SchemaNode_ToJson()
    {
        var ctx  = ServiceProvider.GetRequiredService<SchemaContext>();
        var intT = (await ctx.GetSchemaTypeAsync<ScalarType>(NS_SYSTEM_INT))!;
        var node = intT.CreateNode(123);
        Assert.IsNotNull(node);

        var json = node.ToJson();
        Assert.IsNotNull(json);
        Assert.AreEqual(123L, (long)json!);
    }

    /// <summary>
    /// StructTypeNode.ToJson() includes all fields
    /// </summary>
    [TestMethod]
    public async Task StructNode_ToJson()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name   = "test.jsontest",
            Type   = SchemaType.Struct,
            Struct = new StructSchema
            {
                Fields =
                [
                    new StructFieldSchema { Name = "a", Type = NS_SYSTEM_STRING },
                    new StructFieldSchema { Name = "b", Type = NS_SYSTEM_INT }
                ]
            }
        });

        var structType = (await ctx.GetSchemaTypeAsync<StructType>("test.jsontest"))!;
        var node       = structType.CreateNode() as StructTypeNode;
        Assert.IsNotNull(node);

        node["a"] = "foo";
        node["b"] = 99;

        var json = node.ToJson() as JsonObject;
        Assert.IsNotNull(json);
        Assert.AreEqual("foo", (string?)json["a"]);
        Assert.AreEqual(99L,   (long?)  json["b"]);
    }
}
