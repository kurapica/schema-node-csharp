using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Context;
using SchemaNode.Schema;
using SchemaNode.Schema.Provider;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.UnitTest.App;

[TestClass]
public class AppDataTest : Base.AppTestBase
{
    [TestMethod]
    public async Task AppData_SaveAndLoadApp()
    {
        var ctx = Provider.GetRequiredService<SchemaContext>();

        await ctx.SaveAppSchemaAsync(new AppSchema { Name = "test" });
        await ctx.SaveAppFieldSchemaAsync("test", new AppFieldSchema { Name = "value", Type = NS_SYSTEM_STRING });

        var appType = await ctx.GetAppTypeAsync("test");
        Assert.IsNotNull(appType);
        var fields = appType.GetFields().ToList();
        Assert.IsTrue(fields.Any(f => f.Name == "value"));
    }

    [TestMethod]
    public async Task AppData_SubApp()
    {
        var ctx = Provider.GetRequiredService<SchemaContext>();
        await ctx.SaveAppSchemaAsync(new AppSchema { Name = "crm" });
        await ctx.SaveAppSchemaAsync(new AppSchema { Name = "crm.customers" });

        var child = await ctx.GetAppTypeAsync("crm.customers");
        Assert.IsNotNull(child);
        Assert.AreEqual("crm.customers", child.Name);
    }

    [TestMethod]
    public async Task AppData_DeleteEmptyApp()
    {
        var ctx = Provider.GetRequiredService<SchemaContext>();
        await ctx.SaveAppSchemaAsync(new AppSchema { Name = "temp" });

        var result = await ctx.DeleteAppSchemaAsync("temp");
        Assert.IsTrue(result);
    }
}
