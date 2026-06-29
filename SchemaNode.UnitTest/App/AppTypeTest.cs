using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Context;
using SchemaNode.Schema;
using SchemaNode.Schema.Provider;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.UnitTest.App;

[TestClass]
public class AppTypeTest : Base.AppTestBase
{
    [TestMethod]
    public async Task AppType_SaveAndLoad()
    {
        var ctx = Provider.GetRequiredService<SchemaContext>();
        await ctx.SaveAppSchemaAsync(new AppSchema { Name = "myapp" });

        var appType = await ctx.GetAppTypeAsync("myapp");
        Assert.IsNotNull(appType);
        Assert.AreEqual("myapp", appType.Name);
    }

    [TestMethod]
    public async Task AppType_WithFields()
    {
        var ctx = Provider.GetRequiredService<SchemaContext>();
        await ctx.SaveAppSchemaAsync(new AppSchema { Name = "scalarapp" });
        await ctx.SaveAppFieldSchemaAsync("scalarapp", new AppFieldSchema
        {
            Name = "score",
            Type = NS_SYSTEM_INT
        });

        var appType = await ctx.GetAppTypeAsync("scalarapp");
        Assert.IsNotNull(appType);
        var fields = appType.GetFields().ToList();
        Assert.IsTrue(fields.Any(f => f.Name == "score"));
    }

    [TestMethod]
    public async Task AppType_SubApp()
    {
        var ctx = Provider.GetRequiredService<SchemaContext>();
        await ctx.SaveAppSchemaAsync(new AppSchema { Name = "catalog" });
        await ctx.SaveAppSchemaAsync(new AppSchema { Container = "catalog", Name = "products" });

        var child = await ctx.GetAppTypeAsync("catalog.products");
        Assert.IsNotNull(child);
        Assert.AreEqual("catalog.products", child.Name);
    }

    [TestMethod]
    public async Task AppType_DeleteField()
    {
        var ctx = Provider.GetRequiredService<SchemaContext>();
        await ctx.SaveAppSchemaAsync(new AppSchema { Name = "delapp" });
        await ctx.SaveAppFieldSchemaAsync("delapp", new AppFieldSchema
        {
            Name = "temp",
            Type = NS_SYSTEM_STRING
        });

        var appType = await ctx.GetAppTypeAsync("delapp");
        Assert.IsNotNull(appType);
        Assert.IsTrue(appType.GetFields().Any(f => f.Name == "temp"));

        bool deleted = await ctx.DeleteAppFieldSchemaAsync("delapp", "temp");
        Assert.IsTrue(deleted);
    }

    [TestMethod]
    public async Task AppType_SwapFields()
    {
        var ctx = Provider.GetRequiredService<SchemaContext>();
        await ctx.SaveAppSchemaAsync(new AppSchema { Name = "swapapp" });
        await ctx.SaveAppFieldSchemaAsync("swapapp", new AppFieldSchema
        {
            Name = "first",
            Type = NS_SYSTEM_INT
        });
        await ctx.SaveAppFieldSchemaAsync("swapapp", new AppFieldSchema
        {
            Name = "second",
            Type = NS_SYSTEM_STRING
        });

        bool swapped = await ctx.SwapAppFieldSchemaAsync("swapapp", "first", "second");
        Assert.IsTrue(swapped);
    }

    [TestMethod]
    public async Task AppType_DeleteApp()
    {
        var ctx = Provider.GetRequiredService<SchemaContext>();
        await ctx.SaveAppSchemaAsync(new AppSchema { Name = "todelete" });

        var appType = await ctx.GetAppTypeAsync("todelete");
        Assert.IsNotNull(appType);

        bool deleted = await ctx.DeleteAppSchemaAsync("todelete");
        Assert.IsTrue(deleted);
    }

    [TestMethod]
    public async Task AppType_SaveWorkflow()
    {
        var ctx = Provider.GetRequiredService<SchemaContext>();
        await ctx.SaveAppSchemaAsync(new AppSchema { Name = "wfapp" });

        bool saved = await ctx.SaveAppWorkflowSchemaAsync("wfapp", new AppWorkflowSchema
        {
            Name = "approval",
            Nodes =
            [
                new() { Name = "start", Type = "system.workflow.control.start" },
                new() { Name = "end", Type = "system.workflow.control.end" }
            ]
        });
        Assert.IsTrue(saved);

        bool deleted = await ctx.DeleteAppWorkflowSchemaAsync("wfapp", "approval");
        Assert.IsTrue(deleted);
    }
}
