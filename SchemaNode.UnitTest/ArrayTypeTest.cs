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
/// Tests for ArrayType: save/load, add/iterate, and JSON serialization
/// </summary>
[TestClass]
public class ArrayTypeTest : TestBase
{
    /// <summary>
    /// Save an ArrayType and verify the element type and primary key are correct
    /// </summary>
    [TestMethod]
    public async Task ArrayType_SaveAndLoad()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name   = "test.itemtype",
            Type   = SchemaType.Struct,
            Struct = new StructSchema
            {
                Fields =
                [
                    new StructFieldSchema { Name = "code",  Type = NS_SYSTEM_STRING },
                    new StructFieldSchema { Name = "value", Type = NS_SYSTEM_INT }
                ]
            }
        });

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name  = "test.itemlist",
            Type  = SchemaType.Array,
            Array = new ArraySchema
            {
                Element = "test.itemtype",
                Primary = ["code"]
            }
        });

        var arrType = await ctx.GetSchemaTypeAsync<ArrayType>("test.itemlist");
        Assert.IsNotNull(arrType);
        Assert.AreEqual(SchemaType.Array, arrType.Type);
        Assert.AreEqual("test.itemtype", arrType.Element);
        Assert.IsNotNull(arrType.Primary);
        Assert.AreEqual("code", arrType.Primary![0]);
    }

    /// <summary>
    /// ArrayTypeNode: add and iterate elements
    /// </summary>
    [TestMethod]
    public async Task ArrayNode_AddAndIterate()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name   = "test.tagtype",
            Type   = SchemaType.Struct,
            Struct = new StructSchema
            {
                Fields = [new StructFieldSchema { Name = "tag", Type = NS_SYSTEM_STRING }]
            }
        });

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name  = "test.tags",
            Type  = SchemaType.Array,
            Array = new ArraySchema { Element = "test.tagtype", Primary = ["tag"] }
        });

        var arrType = await ctx.GetSchemaTypeAsync<ArrayType>("test.tags");
        Assert.IsNotNull(arrType);

        var arrNode = new ArrayTypeNode(arrType);
        arrNode[0] = new JsonObject { ["tag"] = "csharp" };
        arrNode[1] = new JsonObject { ["tag"] = "dotnet" };

        Assert.AreEqual(2, arrNode.Count);

        var first = arrNode[0] as StructTypeNode;
        Assert.IsNotNull(first);
        Assert.AreEqual("csharp", first.GetField("tag")?.ToValue<string>());
    }

    /// <summary>
    /// ArrayTypeNode.ToJson() serializes correctly into a JsonArray
    /// </summary>
    [TestMethod]
    public async Task ArrayNode_ToJson()
    {
        var ctx      = ServiceProvider.GetRequiredService<SchemaContext>();
        var intsType = await ctx.GetSchemaTypeAsync<ArrayType>(NS_SYSTEM_INTS);
        Assert.IsNotNull(intsType);

        var arrNode = new ArrayTypeNode(intsType);
        arrNode[0] = 10;
        arrNode[1] = 20;
        arrNode[2] = 30;

        var json = arrNode.ToJson() as JsonArray;
        Assert.IsNotNull(json);
        Assert.AreEqual(3, json.Count);
        Assert.AreEqual(10L, (long?)json[0]);
        Assert.AreEqual(20L, (long?)json[1]);
        Assert.AreEqual(30L, (long?)json[2]);
    }
}
