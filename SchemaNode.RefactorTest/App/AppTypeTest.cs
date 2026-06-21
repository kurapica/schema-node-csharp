using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Context;
using SchemaNode.Schema;
using SchemaNode.Schema.Provider;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.RefactorTest.App;

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
        await ctx.SaveAppSchemaAsync(new AppSchema { Name = "catalog.products" });

        var child = await ctx.GetAppTypeAsync("catalog.products");
        Assert.IsNotNull(child);
        Assert.AreEqual("catalog.products", child.Name);
    }
}
