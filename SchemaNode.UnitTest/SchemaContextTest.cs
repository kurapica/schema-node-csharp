using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Api.Schema.Application;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.UnitTest;

/// <summary>
/// Tests for SchemaContext: context items, schema deletion
/// </summary>
[TestClass]
public class SchemaContextTest : TestBase
{
    // ─────────────────────────────────────────────────────────────────────
    // Context items
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// SetContextItem / GetContextItem generic store and retrieve
    /// </summary>
    [TestMethod]
    public void ContextItem_SetAndGet()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        ctx.SetContextItem("hello-context");
        var val = ctx.GetContextItem<string>();
        Assert.AreEqual("hello-context", val);
    }

    /// <summary>
    /// An unset context item returns null
    /// </summary>
    [TestMethod]
    public void ContextItem_GetMissing_ReturnsNull()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();
        var val = ctx.GetContextItem<List<int>>();
        Assert.IsNull(val);
    }

    /// <summary>
    /// Verify TryGetContextItem behavior
    /// </summary>
    [TestMethod]
    public void ContextItem_TryGet()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        ctx.SetContextItem("answer-42");
        bool found = ctx.TryGetContextItem<string>(out var val);
        Assert.IsTrue(found);
        Assert.AreEqual("answer-42", val);
    }

    /// <summary>
    /// GetOrCreateContextItem creates automatically when absent; subsequent calls return the same instance
    /// </summary>
    [TestMethod]
    public void ContextItem_GetOrCreate()
    {
        var ctx     = ServiceProvider.GetRequiredService<SchemaContext>();
        var created = ctx.GetOrCreateContextItem<List<string>>();
        Assert.IsNotNull(created);

        var again = ctx.GetOrCreateContextItem<List<string>>();
        Assert.AreSame(created, again);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Schema deletion
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// After saving and deleting a schema, lookup should return null
    /// </summary>
    [TestMethod]
    public async Task Schema_Delete()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name   = "test.deleteme",
            Type   = SchemaType.Struct,
            Struct = new StructSchema
            {
                Fields = [new StructFieldSchema { Name = "x", Type = NS_SYSTEM_INT }]
            }
        });

        var before = await ctx.GetSchemaTypeAsync("test.deleteme");
        Assert.IsNotNull(before, "Schema should exist before deletion");

        bool deleted = await ctx.DeleteSchemaAsync("test.deleteme");
        Assert.IsTrue(deleted);

        var after = await ctx.GetSchemaTypeAsync("test.deleteme");
        Assert.IsNull(after, "Schema should not exist after deletion");
    }
}
