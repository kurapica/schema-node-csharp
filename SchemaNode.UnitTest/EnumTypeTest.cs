using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Api.Schema.Application;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Function;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.UnitTest;

/// <summary>
/// Tests for EnumType: basic enum, cascade enum, and system.data.enum functions
/// </summary>
[TestClass]
public class EnumTypeTest : TestBase
{
    // ─────────────────────────────────────────────────────────────────────
    // Basic EnumType
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Save an integer enum schema and verify its loaded structure
    /// </summary>
    [TestMethod]
    public async Task EnumType_SaveAndLoad()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.color",
            Type = SchemaType.Enum,
            Enum = new EnumSchema
            {
                Type = EnumValueType.Int,
                Values =
                [
                    new EnumValueInfo { Value = "1", Name = "Red" },
                    new EnumValueInfo { Value = "2", Name = "Green" },
                    new EnumValueInfo { Value = "3", Name = "Blue" }
                ]
            }
        });

        var enumType = await ctx.GetSchemaTypeAsync<EnumType>("test.color");
        Assert.IsNotNull(enumType);
        Assert.AreEqual(SchemaType.Enum, enumType.Type);
        Assert.AreEqual(EnumValueType.Int, enumType.ValueType);
        Assert.AreEqual(3, (await enumType.LoadEnumSubListAsync(ctx, ""))?.Length ?? 0);
    }

    /// <summary>
    /// Enum node can store and retrieve values correctly
    /// </summary>
    [TestMethod]
    public async Task EnumNode_SetAndGetValue()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.status",
            Type = SchemaType.Enum,
            Enum = new EnumSchema
            {
                Type = EnumValueType.String,
                Values =
                [
                    new EnumValueInfo { Value = "active",   Name = "Active" },
                    new EnumValueInfo { Value = "inactive", Name = "Inactive" }
                ]
            }
        });

        var enumType = await ctx.GetSchemaTypeAsync<EnumType>("test.status");
        Assert.IsNotNull(enumType);

        var node = enumType.CreateNode("active");
        Assert.IsNotNull(node);
        Assert.AreEqual("active", node.ToValue<string>());
    }

    // ─────────────────────────────────────────────────────────────────────
    // Cascading enum values
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Save a 2-level cascade enum and verify the Cascade labels and root values are loaded correctly
    /// </summary>
    [TestMethod]
    public async Task CascadeEnum_Schema_CascadeLabelsAndRootValues()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.region",
            Type = SchemaType.Enum,
            Enum = new EnumSchema
            {
                Type    = EnumValueType.String,
                Cascade = ["Country", "City"],
                Values  =
                [
                    new EnumValueInfo { Value = "CN", Name = "China" },
                    new EnumValueInfo { Value = "US", Name = "USA"   }
                ]
            }
        });

        var enumType = await ctx.GetSchemaTypeAsync<EnumType>("test.region");
        Assert.IsNotNull(enumType);
        Assert.AreEqual(2, enumType.Cascade?.Length,    "Cascade should have 2 labels");
        Assert.AreEqual("Country", (string)enumType.Cascade![0]);
        Assert.AreEqual("City",    (string)enumType.Cascade![1]);
        Assert.AreEqual(2, (await enumType.LoadEnumSubListAsync(ctx, ""))?.Length ?? 0, "Root should have 2 top-level values");
        Assert.IsTrue((await enumType.LoadEnumSubListAsync(ctx, ""))!.Any(v => v.Value == "CN"));
        Assert.IsTrue((await enumType.LoadEnumSubListAsync(ctx, ""))!.Any(v => v.Value == "US"));
    }

    /// <summary>
    /// ResetEnumSubListAsync stores child values under a parent; LoadEnumSubListAsync retrieves them and HasSubList is set
    /// </summary>
    [TestMethod]
    public async Task CascadeEnum_SaveSubList_LoadChildren()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.region",
            Type = SchemaType.Enum,
            Enum = new EnumSchema
            {
                Type    = EnumValueType.String,
                Cascade = ["Country", "City"],
                Values  =
                [
                    new EnumValueInfo { Value = "CN", Name = "China" },
                    new EnumValueInfo { Value = "US", Name = "USA"   }
                ]
            }
        });

        var enumType = await ctx.GetSchemaTypeAsync<EnumType>("test.region");
        Assert.IsNotNull(enumType);

        bool saved = await ctx.SaveEnumSubListAsync("test.region", "CN",
        [
            new EnumValueInfo { Value = "BJ", Name = "Beijing"  },
            new EnumValueInfo { Value = "SH", Name = "Shanghai" }
        ], false);
        Assert.IsTrue(saved);

        var cities = await enumType.LoadEnumSubListAsync(ctx, "CN", false);
        Assert.AreEqual(2, cities.Length);
        Assert.IsTrue(cities.Any(c => c.Value == "BJ"));
        Assert.IsTrue(cities.Any(c => c.Value == "SH"));

        var cnNode = (await enumType.LoadEnumSubListAsync(ctx, ""))?.FirstOrDefault(v => v.Value == "CN");
        Assert.IsNotNull(cnNode);
        Assert.IsTrue(cnNode.HasSubList ?? false, "CN should have HasSubList = true after saving children");
    }

    /// <summary>
    /// Appending to a sub-list adds new values without removing existing ones
    /// </summary>
    [TestMethod]
    public async Task CascadeEnum_AppendSubList()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.region",
            Type = SchemaType.Enum,
            Enum = new EnumSchema
            {
                Type    = EnumValueType.String,
                Cascade = ["Country", "City"],
                Values  = [new EnumValueInfo { Value = "CN", Name = "China" }]
            }
        });

        var enumType = await ctx.GetSchemaTypeAsync<EnumType>("test.region");
        Assert.IsNotNull(enumType);

        await ctx.SaveEnumSubListAsync("test.region", "CN",
        [
            new EnumValueInfo { Value = "BJ", Name = "Beijing"  },
            new EnumValueInfo { Value = "SH", Name = "Shanghai" }
        ], false);

        await ctx.SaveEnumSubListAsync("test.region", "CN",
        [
            new EnumValueInfo { Value = "GZ", Name = "Guangzhou" }
        ], true);

        var cities = await enumType.LoadEnumSubListAsync(ctx, "CN", false);
        Assert.AreEqual(3, cities.Length);
        Assert.IsTrue(cities.Any(c => c.Value == "BJ"));
        Assert.IsTrue(cities.Any(c => c.Value == "SH"));
        Assert.IsTrue(cities.Any(c => c.Value == "GZ"));
    }

    /// <summary>
    /// Replacing a sub-list (append = false) removes values absent from the new set
    /// </summary>
    [TestMethod]
    public async Task CascadeEnum_ReplaceSubList()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.region",
            Type = SchemaType.Enum,
            Enum = new EnumSchema
            {
                Type    = EnumValueType.String,
                Cascade = ["Country", "City"],
                Values  = [new EnumValueInfo { Value = "CN", Name = "China" }]
            }
        });

        var enumType = await ctx.GetSchemaTypeAsync<EnumType>("test.region");
        Assert.IsNotNull(enumType);

        await ctx.SaveEnumSubListAsync("test.region", "CN",
        [
            new EnumValueInfo { Value = "BJ", Name = "Beijing"  },
            new EnumValueInfo { Value = "SH", Name = "Shanghai" }
        ], false);

        // Replace: keep only BJ
        await ctx.SaveEnumSubListAsync("test.region", "CN",
        [
            new EnumValueInfo { Value = "BJ", Name = "Beijing" }
        ], false);

        var cities = await enumType.LoadEnumSubListAsync(ctx, "CN", false);
        Assert.AreEqual(1, cities.Length);
        Assert.AreEqual("BJ", cities[0].Value);
    }

    /// <summary>
    /// GetEnumAccesses returns the full path from the virtual root down to the requested leaf value
    /// </summary>
    [TestMethod]
    public async Task CascadeEnum_GetEnumAccesses_PathNavigation()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.region",
            Type = SchemaType.Enum,
            Enum = new EnumSchema
            {
                Type    = EnumValueType.String,
                Cascade = ["Country", "City"],
                Values  =
                [
                    new EnumValueInfo { Value = "CN", Name = "China" },
                    new EnumValueInfo { Value = "US", Name = "USA"   }
                ]
            }
        });

        var enumType = await ctx.GetSchemaTypeAsync<EnumType>("test.region");
        Assert.IsNotNull(enumType);

        await ctx.SaveEnumSubListAsync("test.region", "CN",
        [
            new EnumValueInfo { Value = "BJ", Name = "Beijing"  },
            new EnumValueInfo { Value = "SH", Name = "Shanghai" }
        ], false);

        // BJ path: virtual-root ("") → CN → BJ
        var bjAccesses = await enumType.LoadEnumValueAccessAsync(ctx, "BJ");
        Assert.IsNotNull(bjAccesses);
        Assert.AreEqual(3, bjAccesses.Length, "Path should be: root → CN → BJ");
        Assert.AreEqual("CN", bjAccesses[1].Value);
        Assert.AreEqual("BJ", bjAccesses[2].Value);

        // CN path: virtual-root → CN (length 2)
        var cnAccesses = await enumType.LoadEnumValueAccessAsync(ctx, "CN");
        Assert.IsNotNull(cnAccesses);
        Assert.AreEqual(2, cnAccesses.Length);
        Assert.AreEqual("CN", cnAccesses[1].Value);
    }

    /// <summary>
    /// LoadEnumAccessListAsync returns each cascade level's selected value and its sibling sub-list
    /// </summary>
    [TestMethod]
    public async Task CascadeEnum_LoadEnumAccessList()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.region",
            Type = SchemaType.Enum,
            Enum = new EnumSchema
            {
                Type    = EnumValueType.String,
                Cascade = ["Country", "City"],
                Values  =
                [
                    new EnumValueInfo { Value = "CN", Name = "China" },
                    new EnumValueInfo { Value = "US", Name = "USA"   }
                ]
            }
        });

        var enumType = await ctx.GetSchemaTypeAsync<EnumType>("test.region");
        Assert.IsNotNull(enumType);

        await ctx.SaveEnumSubListAsync("test.region", "CN",
        [
            new EnumValueInfo { Value = "BJ", Name = "Beijing"  },
            new EnumValueInfo { Value = "SH", Name = "Shanghai" }
        ], false);

        // For leaf "BJ":
        //   accessList[0] → { Value="CN", Name="Country", SubList=[CN, US] }
        //   accessList[1] → { Value="BJ", Name="City",    SubList=[BJ, SH] }
        var accessList = await enumType.LoadEnumAccessListAsync(ctx, "BJ", false, false);
        Assert.AreEqual(2, accessList.Length);

        Assert.AreEqual("CN",      accessList[0].Value);
        Assert.AreEqual("Country", (string)accessList[0].Name!);
        Assert.AreEqual(2,         accessList[0].SubList?.Length ?? 0, "Country level should list [CN, US]");

        Assert.AreEqual("BJ",   accessList[1].Value);
        Assert.AreEqual("City", (string)accessList[1].Name!);
        Assert.AreEqual(2,      accessList[1].SubList?.Length ?? 0, "City level should list [BJ, SH]");
    }

    /// <summary>
    /// Saving an empty sub-list clears all children and sets HasSubList to false
    /// </summary>
    [TestMethod]
    public async Task CascadeEnum_EmptySubList_ClearsChildren()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.region",
            Type = SchemaType.Enum,
            Enum = new EnumSchema
            {
                Type    = EnumValueType.String,
                Cascade = ["Country", "City"],
                Values  = [new EnumValueInfo { Value = "CN", Name = "China" }]
            }
        });

        var enumType = await ctx.GetSchemaTypeAsync<EnumType>("test.region");
        Assert.IsNotNull(enumType);

        await ctx.SaveEnumSubListAsync("test.region", "CN",
        [
            new EnumValueInfo { Value = "BJ", Name = "Beijing" }
        ], false);

        Assert.AreEqual(1, (await enumType.LoadEnumSubListAsync(ctx, "CN", false)).Length);

        // Save empty list — should clear all children
        await ctx.SaveEnumSubListAsync("test.region", "CN", [], false);

        var cnNode = (await enumType.LoadEnumSubListAsync(ctx, ""))?.FirstOrDefault(v => v.Value == "CN");
        Assert.IsNotNull(cnNode);
        Assert.IsFalse(cnNode.HasSubList ?? false, "CN should have HasSubList = false after clearing");

        var afterClear = await enumType.LoadEnumSubListAsync(ctx, "CN", false);
        Assert.AreEqual(0, afterClear.Length, "No children should remain after clearing");
    }

    /// <summary>
    /// ValidateValueAsync accepts a known cascade leaf value and rejects an unknown value
    /// </summary>
    [TestMethod]
    public async Task CascadeEnum_ValidateValue()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.region",
            Type = SchemaType.Enum,
            Enum = new EnumSchema
            {
                Type    = EnumValueType.String,
                Cascade = ["Country", "City"],
                Values  = [new EnumValueInfo { Value = "CN", Name = "China" }]
            }
        });

        var enumType = await ctx.GetSchemaTypeAsync<EnumType>("test.region");
        Assert.IsNotNull(enumType);

        await ctx.SaveEnumSubListAsync("test.region", "CN",
        [
            new EnumValueInfo { Value = "BJ", Name = "Beijing" }
        ], false);

        // "BJ" exists in the cascade tree → valid
        var (validNode, validError) = await enumType.ValidateValueAsync(ctx, JsonValue.Create("BJ")!);
        Assert.IsNull(validError,   "BJ is a known cascade leaf and should be valid");
        Assert.IsNotNull(validNode, "A valid cascade value should return a non-null node");

        // "XX" does not exist → invalid
        var (invalidNode, invalidError) = await enumType.ValidateValueAsync(ctx, JsonValue.Create("XX")!);
        Assert.IsNull(invalidNode,    "XX does not exist in the cascade tree");
        Assert.IsNotNull(invalidError, "An unknown cascade value should return an error");
    }

    // ─────────────────────────────────────────────────────────────────────
    // system.data.enum functions
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// isdescendant returns true when the value exists anywhere in the ancestor path of the root
    /// </summary>
    [TestMethod]
    public async Task EnumOper_IsDescendant_True()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();
        await SetupGeo3EnumAsync(ctx);

        // BJ → CN → AS: BJ is a descendant of both CN and AS
        Assert.IsTrue(await SystemData.EnumOper.isdescendant(ctx, "test.geo3", "BJ", "AS"), "BJ should be a descendant of AS");
        Assert.IsTrue(await SystemData.EnumOper.isdescendant(ctx, "test.geo3", "BJ", "CN"), "BJ should be a descendant of CN");
        // A value is always a descendant of itself
        Assert.IsTrue(await SystemData.EnumOper.isdescendant(ctx, "test.geo3", "BJ", "BJ"), "BJ should be a descendant of itself");
        // Country-level: CN is a descendant of AS
        Assert.IsTrue(await SystemData.EnumOper.isdescendant(ctx, "test.geo3", "CN", "AS"), "CN should be a descendant of AS");
    }

    /// <summary>
    /// isdescendant returns false when the root is not an ancestor of the value
    /// </summary>
    [TestMethod]
    public async Task EnumOper_IsDescendant_False()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();
        await SetupGeo3EnumAsync(ctx);

        // BJ is under AS/CN, not under EU/DE
        Assert.IsFalse(await SystemData.EnumOper.isdescendant(ctx, "test.geo3", "BJ", "EU"), "BJ is not a descendant of EU");
        Assert.IsFalse(await SystemData.EnumOper.isdescendant(ctx, "test.geo3", "BJ", "JP"), "BJ is not a descendant of JP");
        // AS is an ancestor of BJ, not the other way around
        Assert.IsFalse(await SystemData.EnumOper.isdescendant(ctx, "test.geo3", "AS", "CN"), "AS is not a descendant of CN");
        // empty/null values
        Assert.IsFalse(await SystemData.EnumOper.isdescendant(ctx, "test.geo3", "", "AS"),   "empty value should return false");
        Assert.IsFalse(await SystemData.EnumOper.isdescendant(ctx, "test.geo3", "BJ", ""),   "empty root should return false");
    }

    /// <summary>
    /// isdescendantany returns true when the value is a descendant of at least one root in the list
    /// </summary>
    [TestMethod]
    public async Task EnumOper_IsDescendantAny()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();
        await SetupGeo3EnumAsync(ctx);

        // BJ is a descendant of CN, which is in the list
        Assert.IsTrue(await SystemData.EnumOper.isdescendantany(ctx, "test.geo3", "BJ", ["CN", "JP"]), "BJ should match CN in the list");
        // BJ is a descendant of AS at continent level
        Assert.IsTrue(await SystemData.EnumOper.isdescendantany(ctx, "test.geo3", "BJ", ["EU", "AS"]), "BJ should match AS in the list");
        // value present in the roots list itself (short-circuit)
        Assert.IsTrue(await SystemData.EnumOper.isdescendantany(ctx, "test.geo3", "BJ", ["BJ", "SH"]), "BJ equals one of the roots");
        // no matching root
        Assert.IsFalse(await SystemData.EnumOper.isdescendantany(ctx, "test.geo3", "BJ", ["EU", "DE"]), "BJ has no ancestor in [EU, DE]");
        // empty roots list
        Assert.IsFalse(await SystemData.EnumOper.isdescendantany(ctx, "test.geo3", "BJ", []), "empty roots should return false");
        // BN (Berlin) is a descendant of DE and EU
        Assert.IsTrue(await SystemData.EnumOper.isdescendantany(ctx, "test.geo3", "BN", ["DE", "JP"]), "BN should match DE in the list");
    }

    /// <summary>
    /// depth returns the correct level (root = 0) for each node in the hierarchy
    /// </summary>
    [TestMethod]
    public async Task EnumOper_Depth()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();
        await SetupGeo3EnumAsync(ctx);

        Assert.AreEqual(0L, await SystemData.EnumOper.depth(ctx, "test.geo3", "AS"), "continent AS should have depth 0");
        Assert.AreEqual(0L, await SystemData.EnumOper.depth(ctx, "test.geo3", "EU"), "continent EU should have depth 0");
        Assert.AreEqual(1L, await SystemData.EnumOper.depth(ctx, "test.geo3", "CN"), "country CN should have depth 1");
        Assert.AreEqual(1L, await SystemData.EnumOper.depth(ctx, "test.geo3", "JP"), "country JP should have depth 1");
        Assert.AreEqual(2L, await SystemData.EnumOper.depth(ctx, "test.geo3", "BJ"), "city BJ should have depth 2");
        Assert.AreEqual(2L, await SystemData.EnumOper.depth(ctx, "test.geo3", "SH"), "city SH should have depth 2");
        // empty value returns -1
        Assert.AreEqual(-1L, await SystemData.EnumOper.depth(ctx, "test.geo3", ""),  "empty value should return -1");
    }

    /// <summary>
    /// parent returns the ancestor at the specified depth level;
    /// negative depth counts back from the value itself (-1 = direct parent)
    /// </summary>
    [TestMethod]
    public async Task EnumOper_Parent()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();
        await SetupGeo3EnumAsync(ctx);

        // For BJ (access path: [AS, CN, BJ]):
        Assert.AreEqual("AS", await SystemData.EnumOper.parent(ctx, "test.geo3", "BJ", 0),  "depth 0 of BJ should be AS (continent)");
        Assert.AreEqual("CN", await SystemData.EnumOper.parent(ctx, "test.geo3", "BJ", 1),  "depth 1 of BJ should be CN (country)");
        Assert.AreEqual("BJ", await SystemData.EnumOper.parent(ctx, "test.geo3", "BJ", 2),  "depth 2 of BJ should be BJ itself");
        Assert.AreEqual("",   await SystemData.EnumOper.parent(ctx, "test.geo3", "BJ", 3),  "depth 3 exceeds actual depth, should return empty");
        // negative depth: -1 = direct parent (one level up from the value)
        Assert.AreEqual("CN", await SystemData.EnumOper.parent(ctx, "test.geo3", "BJ", -1), "depth -1 of BJ should be CN (direct parent)");
        // For AS (access path: [AS]): depth 0 = AS itself
        Assert.AreEqual("AS", await SystemData.EnumOper.parent(ctx, "test.geo3", "AS", 0),  "depth 0 of AS should be AS itself");
        // empty value returns empty string
        Assert.AreEqual("",   await SystemData.EnumOper.parent(ctx, "test.geo3", "", 0),    "empty value should return empty string");
    }

    /// <summary>
    /// lca returns the lowest common ancestor of a set of values
    /// </summary>
    [TestMethod]
    public async Task EnumOper_Lca()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();
        await SetupGeo3EnumAsync(ctx);

        // BJ and SH are siblings under CN → LCA is CN
        Assert.AreEqual("CN", await SystemData.EnumOper.lca(ctx, "test.geo3", ["BJ", "SH"]), "LCA of BJ and SH should be CN");
        // BJ (under CN) and TK (under JP) are both under AS → LCA is AS
        Assert.AreEqual("AS", await SystemData.EnumOper.lca(ctx, "test.geo3", ["BJ", "TK"]), "LCA of BJ and TK should be AS");
        // BJ (Asia) and BN (Europe) share no common ancestor → LCA is empty
        Assert.AreEqual("",   await SystemData.EnumOper.lca(ctx, "test.geo3", ["BJ", "BN"]), "LCA of BJ and BN should be empty (different continents)");
        // single value → returns the value itself
        Assert.AreEqual("BJ", await SystemData.EnumOper.lca(ctx, "test.geo3", ["BJ"]),        "LCA of a single value should be the value itself");
        // empty input → returns empty string
        Assert.AreEqual("",   await SystemData.EnumOper.lca(ctx, "test.geo3", []),             "LCA of empty set should be empty string");
        // all three cities in China → LCA is CN
        Assert.AreEqual("CN", await SystemData.EnumOper.lca(ctx, "test.geo3", ["BJ", "SH", "CN"]), "LCA of BJ, SH and CN should be CN");
    }

    // ─────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a 3-level cascade enum "test.geo3" (Continent → Country → City)
    /// with the following hierarchy:
    ///   AS (Asia)   → CN (China)  → BJ (Beijing), SH (Shanghai)
    ///               → JP (Japan)  → TK (Tokyo)
    ///   EU (Europe) → DE (Germany)→ BN (Berlin)
    /// </summary>
    private static async Task SetupGeo3EnumAsync(SchemaContext ctx)
    {
        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.geo3",
            Type = SchemaType.Enum,
            Enum = new EnumSchema
            {
                Type    = EnumValueType.String,
                Cascade = ["Continent", "Country", "City"],
                Values  =
                [
                    new EnumValueInfo { Value = "AS", Name = "Asia" },
                    new EnumValueInfo { Value = "EU", Name = "Europe" }
                ]
            }
        });

        // Level 1 → Level 2
        await ctx.SaveEnumSubListAsync("test.geo3", "AS",
        [
            new EnumValueInfo { Value = "CN", Name = "China" },
            new EnumValueInfo { Value = "JP", Name = "Japan"}
        ], false);

        await ctx.SaveEnumSubListAsync("test.geo3", "EU",
        [
            new EnumValueInfo { Value = "DE", Name = "Germany" }
        ], false);

        // Level 2 → Level 3
        await ctx.SaveEnumSubListAsync("test.geo3", "CN",
        [
            new EnumValueInfo { Value = "BJ", Name = "Beijing"  },
            new EnumValueInfo { Value = "SH", Name = "Shanghai" }
        ], false);

        await ctx.SaveEnumSubListAsync("test.geo3", "JP",
        [
            new EnumValueInfo { Value = "TK", Name = "Tokyo" }
        ], false);

        await ctx.SaveEnumSubListAsync("test.geo3", "DE",
        [
            new EnumValueInfo { Value = "BN", Name = "Berlin" }
        ], false);
    }
}
