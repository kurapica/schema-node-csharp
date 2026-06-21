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
/// Tests for AppType: save/load, field management, and sub-app
/// </summary>
[TestClass]
public class AppTypeTest : TestBase
{
    /// <summary>
    /// Save an AppSchema and verify the AppType is loaded correctly
    /// </summary>
    [TestMethod]
    public async Task AppType_SaveAndLoad()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveAppSchemaAsync(new AppSchema { Name = "myapp" });

        var appType = await ctx.GetAppTypeAsync("myapp");
        Assert.IsNotNull(appType);
        Assert.AreEqual("myapp", appType.Name);
    }

    /// <summary>
    /// Add a field to an app and verify the field list
    /// </summary>
    [TestMethod]
    public async Task AppType_SaveAndLoadFields()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveAppSchemaAsync(new AppSchema
        {
            Name        = "scalarapp",
            ScopePolicy = new AppScopePolicy { Type = AppScopeType.SystemLevel }
        });

        await ctx.SaveAppFieldSchemaAsync("scalarapp", new AppFieldSchema
        {
            Name = "score",
            Type = NS_SYSTEM_INT
        });

        var appType = await ctx.GetAppTypeAsync("scalarapp");
        Assert.IsNotNull(appType);
        Assert.IsNotNull(appType.Fields);
        Assert.IsTrue(appType.Fields.Any(f => f.Name == "score"));
    }

    /// <summary>
    /// Create and load a nested AppSchema (sub-app)
    /// </summary>
    [TestMethod]
    public async Task AppType_SubApp()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveAppSchemaAsync(new AppSchema { Name = "catalog" });
        await ctx.SaveAppSchemaAsync(new AppSchema { Name = "catalog.products" });

        var parent = await ctx.GetAppTypeAsync("catalog");
        var child  = await ctx.GetAppTypeAsync("catalog.products");

        Assert.IsNotNull(parent);
        Assert.IsNotNull(child);
        Assert.AreEqual("catalog.products", child.Name);
    }
}
