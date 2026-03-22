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
/// Tests for StructType: save/load, field operations, and inheritance
/// </summary>
[TestClass]
public class StructTypeTest : TestBase
{
    /// <summary>
    /// Save a StructType and verify the field mapping is correct
    /// </summary>
    [TestMethod]
    public async Task StructType_SaveAndLoad()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name   = "test.person",
            Type   = SchemaType.Struct,
            Struct = new StructSchema
            {
                Fields =
                [
                    new StructFieldSchema { Name = "name", Type = NS_SYSTEM_STRING },
                    new StructFieldSchema { Name = "age",  Type = NS_SYSTEM_INT }
                ]
            }
        });

        var structType = await ctx.GetSchemaTypeAsync<StructType>("test.person");
        Assert.IsNotNull(structType);
        Assert.AreEqual(SchemaType.Struct, structType.Type);
        Assert.AreEqual(2, structType.Fields.Length);
        Assert.IsTrue(structType.Fields.Any(f => f.Name == "name"));
        Assert.IsTrue(structType.Fields.Any(f => f.Name == "age"));
    }

    /// <summary>
    /// StructTypeNode field read/write operations
    /// </summary>
    [TestMethod]
    public async Task StructNode_SetAndGetFields()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name   = "test.point",
            Type   = SchemaType.Struct,
            Struct = new StructSchema
            {
                Fields =
                [
                    new StructFieldSchema { Name = "x", Type = NS_SYSTEM_INT },
                    new StructFieldSchema { Name = "y", Type = NS_SYSTEM_INT }
                ]
            }
        });

        var structType = await ctx.GetSchemaTypeAsync<StructType>("test.point");
        Assert.IsNotNull(structType);

        var node = structType.CreateNode() as StructTypeNode;
        Assert.IsNotNull(node);

        node["x"] = 10;
        node["y"] = 20;

        Assert.AreEqual(10L, node.GetField("x")?.ToValue<long>());
        Assert.AreEqual(20L, node.GetField("y")?.ToValue<long>());
    }

    /// <summary>
    /// StructType supports inheritance: the child type's BaseNode references the parent type
    /// </summary>
    [TestMethod]
    public async Task StructType_Inheritance()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name   = "test.base",
            Type   = SchemaType.Struct,
            Struct = new StructSchema
            {
                Fields = [new StructFieldSchema { Name = "id", Type = NS_SYSTEM_STRING }]
            }
        });

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name   = "test.child",
            Type   = SchemaType.Struct,
            Struct = new StructSchema
            {
                Base   = "test.base",
                Fields = [new StructFieldSchema { Name = "extra", Type = NS_SYSTEM_INT }]
            }
        });

        var childType = await ctx.GetSchemaTypeAsync<StructType>("test.child");
        Assert.IsNotNull(childType);
        Assert.IsNotNull(childType.BaseNode, "BaseNode should be set");
        Assert.AreEqual("test.base", childType.Base);
    }
}
