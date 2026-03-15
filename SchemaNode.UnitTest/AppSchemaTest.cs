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

[TestClass]
public class AppSchemaTest : TestBase
{
    // ─────────────────────────────────────────────────────────────────────
    // 1. AppSchema – save & load
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Save a minimal app schema and verify it is returned by GetAppTypeAsync
    /// </summary>
    [TestMethod]
    public async Task AppSchema_SaveAndLoad()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        bool saved = await ctx.SaveAppSchemaAsync(new AppSchema
        {
            Name    = "test.orders",
            Display = "Orders",
            ScopePolicy = new AppScopePolicy { Type = AppScopeType.SystemLevel }
        });

        Assert.IsTrue(saved);

        AppType? app = await ctx.GetAppTypeAsync("test.orders");
        Assert.IsNotNull(app);
        Assert.AreEqual("test.orders", app.Name);
        Assert.AreEqual(AppScopeType.SystemLevel, app.ScopeType);
    }

    /// <summary>
    /// Save a parent and a child app schema and verify the hierarchy
    /// </summary>
    [TestMethod]
    public async Task AppSchema_SubApp_Hierarchy()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveAppSchemaAsync(new AppSchema
        {
            Name        = "test.crm",
            ScopePolicy = new AppScopePolicy { Type = AppScopeType.SystemLevel }
        });

        await ctx.SaveAppSchemaAsync(new AppSchema
        {
            Name        = "test.crm.customers",
            ScopePolicy = new AppScopePolicy { Type = AppScopeType.SystemLevel }
        });

        AppType? parent = await ctx.GetAppTypeAsync("test.crm");
        AppType? child  = await ctx.GetAppTypeAsync("test.crm.customers");

        Assert.IsNotNull(parent);
        Assert.IsNotNull(child);
        Assert.AreEqual("test.crm.customers", child.Name);
        Assert.IsTrue(parent.Apps?.Any(a => a.Name.Equals("test.crm.customers", StringComparison.OrdinalIgnoreCase)) == true);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 2. AppSchema – delete
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// An empty app (no fields, no sub-apps) can be deleted
    /// </summary>
    [TestMethod]
    public async Task AppSchema_Delete_EmptyApp_Succeeds()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveAppSchemaAsync(new AppSchema
        {
            Name        = "test.deleteme",
            ScopePolicy = new AppScopePolicy { Type = AppScopeType.SystemLevel }
        });

        bool deleted = await ctx.DeleteAppSchemaAsync("test.deleteme");
        Assert.IsTrue(deleted);

        AppType? app = await ctx.GetAppTypeAsync("test.deleteme");
        Assert.IsNull(app);
    }

    /// <summary>
    /// An app that has at least one field cannot be deleted (IsUsed = true)
    /// </summary>
    [TestMethod]
    public async Task AppSchema_Delete_WithFields_ReturnsFalse()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveAppSchemaAsync(new AppSchema
        {
            Name        = "test.nodelete",
            ScopePolicy = new AppScopePolicy { Type = AppScopeType.SystemLevel }
        });

        await ctx.SaveAppFieldSchemaAsync("test.nodelete", new AppFieldSchema
        {
            Name = "score",
            Type = NS_SYSTEM_INT
        });

        bool deleted = await ctx.DeleteAppSchemaAsync("test.nodelete");
        Assert.IsFalse(deleted, "App with fields should not be deletable");
    }

    // ─────────────────────────────────────────────────────────────────────
    // 3. AppFieldSchema – save & load
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Save an app field with a scalar type and verify the resolved SchemaType
    /// </summary>
    [TestMethod]
    public async Task AppFieldSchema_Scalar_SaveAndLoad()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveAppSchemaAsync(new AppSchema
        {
            Name        = "test.config",
            ScopePolicy = new AppScopePolicy { Type = AppScopeType.SystemLevel }
        });

        bool saved = await ctx.SaveAppFieldSchemaAsync("test.config", new AppFieldSchema
        {
            Name    = "maxretry",
            Type    = NS_SYSTEM_INT,
            Display = "Max Retry",
            Seqno   = 1
        });

        Assert.IsTrue(saved);

        AppType? app = await ctx.GetAppTypeAsync("test.config");
        Assert.IsNotNull(app);
        Assert.IsTrue(app.Fields?.Any(f => f.Name == "maxretry") == true);

        AppFieldType? field = app.GetField("maxretry");
        Assert.IsNotNull(field);
        Assert.AreEqual(NS_SYSTEM_INT, field.Type);
        Assert.IsInstanceOfType<ScalarType>(field.SchemaType);
        Assert.AreEqual(SchemaNodeStatus.Ready, field.Status ?? SchemaNodeStatus.Ready);
    }

    /// <summary>
    /// Save an app field with an array-of-struct type and verify element type resolution
    /// </summary>
    [TestMethod]
    public async Task AppFieldSchema_Array_SaveAndLoad()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        // Define a struct for the array element
        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name   = "test.orderitem",
            Type   = SchemaType.Struct,
            Struct = new StructSchema
            {
                Fields =
                [
                    new StructFieldSchema { Name = "code",     Type = NS_SYSTEM_STRING },
                    new StructFieldSchema { Name = "quantity", Type = NS_SYSTEM_INT }
                ]
            }
        });

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name  = "test.orderitems",
            Type  = SchemaType.Array,
            Array = new ArraySchema { Element = "test.orderitem", Primary = ["code"] }
        });

        await ctx.SaveAppSchemaAsync(new AppSchema
        {
            Name        = "test.sales",
            ScopePolicy = new AppScopePolicy { Type = AppScopeType.SystemLevel }
        });

        bool saved = await ctx.SaveAppFieldSchemaAsync("test.sales", new AppFieldSchema
        {
            Name = "items",
            Type = "test.orderitems"
        });

        Assert.IsTrue(saved);

        AppType? app = await ctx.GetAppTypeAsync("test.sales");
        AppFieldType? field = app?.GetField("items");

        Assert.IsNotNull(field);
        Assert.IsInstanceOfType<ArrayType>(field.SchemaType);

        ArrayType arrType = (ArrayType)field.SchemaType!;
        Assert.AreEqual("test.orderitem", arrType.Element);
        Assert.IsNotNull(arrType.Primary);
        Assert.AreEqual("code", arrType.Primary![0]);
        Assert.AreEqual(SchemaNodeStatus.Ready, field.Status ?? SchemaNodeStatus.Ready);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 4. AppFieldSchema – field flags
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Frontend and Disable flags are correctly persisted and reflected in AppFieldType
    /// </summary>
    [TestMethod]
    public async Task AppFieldSchema_Flags_ArePreserved()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveAppSchemaAsync(new AppSchema
        {
            Name        = "test.flags",
            ScopePolicy = new AppScopePolicy { Type = AppScopeType.SystemLevel }
        });

        await ctx.SaveAppFieldSchemaAsync("test.flags", new AppFieldSchema
        {
            Name     = "label",
            Type     = NS_SYSTEM_STRING,
            Frontend = true,
            Disable  = false
        });

        await ctx.SaveAppFieldSchemaAsync("test.flags", new AppFieldSchema
        {
            Name      = "note",
            Type      = NS_SYSTEM_STRING,
            Readonly  = true,
            IncrUpdate = true
        });

        AppType? app = await ctx.GetAppTypeAsync("test.flags");

        AppFieldType? labelField = app?.GetField("label");
        Assert.IsNotNull(labelField);
        Assert.IsTrue(labelField.Frontend == true);
        Assert.IsFalse(labelField.EnableDynamicTable, "Frontend-only field should not enable dynamic table");

        AppFieldType? noteField = app?.GetField("note");
        Assert.IsNotNull(noteField);
        Assert.IsTrue(noteField.Readonly == true);
        Assert.IsTrue(noteField.IncrUpdate == true);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 5. AppFieldSchema – invalid type produces wrong-type status
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// SaveAppFieldSchemaAsync throws when a field references a non-existent type
    /// </summary>
    [TestMethod]
    public async Task AppFieldSchema_InvalidType_ThrowsOnSave()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveAppSchemaAsync(new AppSchema
        {
            Name        = "test.broken",
            ScopePolicy = new AppScopePolicy { Type = AppScopeType.SystemLevel }
        });

        bool threw = false;
        try
        {
            await ctx.SaveAppFieldSchemaAsync("test.broken", new AppFieldSchema
            {
                Name = "ghost",
                Type = "test.nonexistent.type"
            });
        }
        catch (Exception)
        {
            threw = true;
        }
        Assert.IsTrue(threw, "Expected an exception when saving a field with a non-existent type");
    }

    // ─────────────────────────────────────────────────────────────────────
    // 6. AppFieldSchema – delete field
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Delete a field and verify it is no longer present in the AppType
    /// </summary>
    [TestMethod]
    public async Task AppFieldSchema_Delete_RemovesField()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveAppSchemaAsync(new AppSchema
        {
            Name        = "test.shrink",
            ScopePolicy = new AppScopePolicy { Type = AppScopeType.SystemLevel }
        });

        await ctx.SaveAppFieldSchemaAsync("test.shrink", new AppFieldSchema { Name = "keep",   Type = NS_SYSTEM_STRING });
        await ctx.SaveAppFieldSchemaAsync("test.shrink", new AppFieldSchema { Name = "remove", Type = NS_SYSTEM_INT });

        // Access the field data first so the underlying dynamic table schema is initialised
        AppType?      app         = await ctx.GetAppTypeAsync("test.shrink");
        AppFieldType? removeField = app!.GetField("remove");
        Assert.IsNotNull(removeField);
        await ctx.GetAppFieldDataAsync(removeField, AppSchemaDataResult.First);

        bool deleted = await ctx.DeleteAppFieldSchemaAsync("test.shrink", "remove");
        Assert.IsTrue(deleted);

        AppType? updated = await ctx.GetAppTypeAsync("test.shrink");
        Assert.IsNull(updated?.GetField("remove"), "Deleted field should not be present");
        Assert.IsNotNull(updated?.GetField("keep"),  "Non-deleted field should still exist");
    }

    // ─────────────────────────────────────────────────────────────────────
    // 7. AppFieldSchema – swap field order
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// SwapAppFieldSchemaAsync changes the seqno so the two fields switch positions
    /// </summary>
    [TestMethod]
    public async Task AppFieldSchema_Swap_ChangesOrder()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveAppSchemaAsync(new AppSchema
        {
            Name        = "test.swappable",
            ScopePolicy = new AppScopePolicy { Type = AppScopeType.SystemLevel }
        });

        await ctx.SaveAppFieldSchemaAsync("test.swappable", new AppFieldSchema { Name = "alpha", Type = NS_SYSTEM_STRING, Seqno = 1 });
        await ctx.SaveAppFieldSchemaAsync("test.swappable", new AppFieldSchema { Name = "beta",  Type = NS_SYSTEM_INT,    Seqno = 2 });

        bool swapped = await ctx.SwapAppFieldSchemaAsync("test.swappable", "alpha", "beta");
        Assert.IsTrue(swapped);

        AppType? app = await ctx.GetAppTypeAsync("test.swappable");
        Assert.IsNotNull(app);

        AppFieldType? alpha = app.GetField("alpha");
        AppFieldType? beta  = app.GetField("beta");
        Assert.IsNotNull(alpha);
        Assert.IsNotNull(beta);
        Assert.IsTrue(alpha.Seqno > beta.Seqno, "alpha should have higher seqno after swap");
    }

    // ─────────────────────────────────────────────────────────────────────
    // 8. AppField data – scalar field save & query
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Write a scalar value to a SystemLevel app field and read it back
    /// </summary>
    [TestMethod]
    public async Task AppField_ScalarField_SaveAndQuery()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveAppSchemaAsync(new AppSchema
        {
            Name        = "test.settings",
            ScopePolicy = new AppScopePolicy { Type = AppScopeType.SystemLevel }
        });

        await ctx.SaveAppFieldSchemaAsync("test.settings", new AppFieldSchema
        {
            Name = "pagesize",
            Type = NS_SYSTEM_INT
        });

        AppType?      app   = await ctx.GetAppTypeAsync("test.settings");
        AppFieldType? field = app!.GetField("pagesize");
        Assert.IsNotNull(field);

        // Write
        bool saved = await ctx.SaveFieldDataAsync(field, field.SchemaType!.CreateNode(20));
        Assert.IsTrue(saved);

        // Read
        (AnySchemaNode? value, _) = await ctx.GetAppFieldDataAsync(field, AppSchemaDataResult.First);
        Assert.IsNotNull(value);
        Assert.AreEqual(20L, value.ToValue<long>());
    }

    /// <summary>
    /// Overwriting a scalar field replaces the previous value
    /// </summary>
    [TestMethod]
    public async Task AppField_ScalarField_Overwrite()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveAppSchemaAsync(new AppSchema
        {
            Name        = "test.overwrite",
            ScopePolicy = new AppScopePolicy { Type = AppScopeType.SystemLevel }
        });

        await ctx.SaveAppFieldSchemaAsync("test.overwrite", new AppFieldSchema
        {
            Name = "title",
            Type = NS_SYSTEM_STRING
        });

        AppType?      app   = await ctx.GetAppTypeAsync("test.overwrite");
        AppFieldType? field = app!.GetField("title");
        Assert.IsNotNull(field);

        await ctx.SaveFieldDataAsync(field, field.SchemaType!.CreateNode("hello"));
        await ctx.SaveFieldDataAsync(field, field.SchemaType!.CreateNode("world"));

        (AnySchemaNode? value, _) = await ctx.GetAppFieldDataAsync(field, AppSchemaDataResult.First);
        Assert.AreEqual("world", value?.ToValue<string>());
    }

    // ─────────────────────────────────────────────────────────────────────
    // 9. AppField data – array-of-struct field save, query & delete
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Save multiple struct records to an array field and query them back as a list
    /// </summary>
    [TestMethod]
    public async Task AppField_ArrayField_SaveAndQueryList()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        // Build element type
        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name   = "test.product",
            Type   = SchemaType.Struct,
            Struct = new StructSchema
            {
                Fields =
                [
                    new StructFieldSchema { Name = "sku",   Type = NS_SYSTEM_STRING },
                    new StructFieldSchema { Name = "price", Type = NS_SYSTEM_INT }
                ]
            }
        });

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name  = "test.products",
            Type  = SchemaType.Array,
            Array = new ArraySchema { Element = "test.product", Primary = ["sku"] }
        });

        await ctx.SaveAppSchemaAsync(new AppSchema
        {
            Name        = "test.catalog",
            ScopePolicy = new AppScopePolicy { Type = AppScopeType.SystemLevel }
        });

        await ctx.SaveAppFieldSchemaAsync("test.catalog", new AppFieldSchema
        {
            Name = "products",
            Type = "test.products"
        });

        AppType?      app   = await ctx.GetAppTypeAsync("test.catalog");
        AppFieldType? field = app!.GetField("products");
        Assert.IsNotNull(field);

        ArrayType   arrType    = (ArrayType)field.SchemaType!;
        StructType  structType = (StructType)arrType.ElementSchemaType!;

        // Build two records
        StructTypeNode row1 = (StructTypeNode)structType.CreateNode()!;
        row1["sku"]   = "A001";
        row1["price"] = 100;

        StructTypeNode row2 = (StructTypeNode)structType.CreateNode()!;
        row2["sku"]   = "B002";
        row2["price"] = 200;

        ArrayTypeNode batch = new ArrayTypeNode(arrType, new AnySchemaNode[] { row1, row2 });

        bool saved = await ctx.SaveFieldDataAsync(field, batch);
        Assert.IsTrue(saved);

        (AnySchemaNode? result, int total) = await ctx.GetAppFieldDataAsync(field, AppSchemaDataResult.List);
        Assert.AreEqual(2, total);
        Assert.IsInstanceOfType<ArrayTypeNode>(result);
        Assert.AreEqual(2, ((ArrayTypeNode)result!).Count);
    }

    /// <summary>
    /// Delete a specific record from an array field by filter
    /// </summary>
    [TestMethod]
    public async Task AppField_ArrayField_DeleteByFilter()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name   = "test.tag",
            Type   = SchemaType.Struct,
            Struct = new StructSchema
            {
                Fields = [new StructFieldSchema { Name = "name", Type = NS_SYSTEM_STRING }]
            }
        });

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name  = "test.taglist",
            Type  = SchemaType.Array,
            Array = new ArraySchema { Element = "test.tag", Primary = ["name"] }
        });

        await ctx.SaveAppSchemaAsync(new AppSchema
        {
            Name        = "test.tagrepo",
            ScopePolicy = new AppScopePolicy { Type = AppScopeType.SystemLevel }
        });

        await ctx.SaveAppFieldSchemaAsync("test.tagrepo", new AppFieldSchema
        {
            Name = "tags",
            Type = "test.taglist"
        });

        AppType?      app   = await ctx.GetAppTypeAsync("test.tagrepo");
        AppFieldType? field = app!.GetField("tags");
        Assert.IsNotNull(field);

        ArrayType  arrType    = (ArrayType)field.SchemaType!;
        StructType structType = (StructType)arrType.ElementSchemaType!;

        StructTypeNode r1 = (StructTypeNode)structType.CreateNode()!;
        r1["name"] = "csharp";
        StructTypeNode r2 = (StructTypeNode)structType.CreateNode()!;
        r2["name"] = "dotnet";

        await ctx.SaveFieldDataAsync(field, new ArrayTypeNode(arrType, new AnySchemaNode[] { r1, r2 }));

        // Delete by filter: name == "csharp"
        ScalarType strType = (await ctx.GetSchemaTypeAsync<ScalarType>(NS_SYSTEM_STRING))!;
        var filter = new AppSchemaDataFilterBinary(
            LogicType.Equal,
            new AppSchemaDataFilterField("name"),
            new AppSchemaDataFilterValue(strType.CreateNode("csharp")!));

        bool deleted = await ctx.DeleteFieldListDataAsync(field, filter);
        Assert.IsTrue(deleted);

        (AnySchemaNode? result, int remaining) = await ctx.GetAppFieldDataAsync(field, AppSchemaDataResult.List);
        Assert.AreEqual(1, remaining);
        ArrayTypeNode remaining1 = (ArrayTypeNode)result!;
        Assert.AreEqual("dotnet", remaining1.Cast<StructTypeNode>().First().GetField("name")?.ToValue<string>());
    }

    // ─────────────────────────────────────────────────────────────────────
    // 10. AppField data – clear all data
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// ClearFieldDataAsync removes all records from an array field
    /// </summary>
    [TestMethod]
    public async Task AppField_ArrayField_ClearAll()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name   = "test.logentry",
            Type   = SchemaType.Struct,
            Struct = new StructSchema
            {
                Fields = [new StructFieldSchema { Name = "msg", Type = NS_SYSTEM_STRING }]
            }
        });

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name  = "test.logentries",
            Type  = SchemaType.Array,
            Array = new ArraySchema { Element = "test.logentry", Primary = ["msg"] }
        });

        await ctx.SaveAppSchemaAsync(new AppSchema
        {
            Name        = "test.logger",
            ScopePolicy = new AppScopePolicy { Type = AppScopeType.SystemLevel }
        });

        await ctx.SaveAppFieldSchemaAsync("test.logger", new AppFieldSchema
        {
            Name       = "logs",
            Type       = "test.logentries",
            AllowClear = true
        });

        AppType?      app   = await ctx.GetAppTypeAsync("test.logger");
        AppFieldType? field = app!.GetField("logs");
        Assert.IsNotNull(field);

        ArrayType  arrType    = (ArrayType)field.SchemaType!;
        StructType structType = (StructType)arrType.ElementSchemaType!;

        StructTypeNode entry = (StructTypeNode)structType.CreateNode()!;
        entry["msg"] = "test log";
        await ctx.SaveFieldDataAsync(field, new ArrayTypeNode(arrType, new AnySchemaNode[] { entry }));

        bool cleared = await ctx.ClearFieldDataAsync(field);
        Assert.IsTrue(cleared);

        (_, int total) = await ctx.GetAppFieldDataAsync(field, AppSchemaDataResult.Count);
        Assert.AreEqual(0, total);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 11. PushAppData – SystemLevel app
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// PushAppDataAsync saves scalar field data for a SystemLevel app
    /// </summary>
    [TestMethod]
    public async Task PushAppData_SystemLevel_ScalarField()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveAppSchemaAsync(new AppSchema
        {
            Name        = "test.push",
            ScopePolicy = new AppScopePolicy { Type = AppScopeType.SystemLevel }
        });

        await ctx.SaveAppFieldSchemaAsync("test.push", new AppFieldSchema
        {
            Name = "counter",
            Type = NS_SYSTEM_INT
        });

        var pushData = new Dictionary<string, AppDataFieldPushQuery>
        {
            ["counter"] = new AppDataFieldPushQuery { Data = 42 }
        };

        (bool pushResult, _) = await ctx.PushAppDataAsync("test.push", null, pushData);
        Assert.IsTrue(pushResult);

        AppType?      app   = await ctx.GetAppTypeAsync("test.push");
        AppFieldType? field = app!.GetField("counter");
        (AnySchemaNode? value, _) = await ctx.GetAppFieldDataAsync(field!, AppSchemaDataResult.First);
        Assert.AreEqual(42L, value?.ToValue<long>());
    }

    /// <summary>
    /// PushAppDataAsync saves array field records for a SystemLevel app
    /// </summary>
    [TestMethod]
    public async Task PushAppData_SystemLevel_ArrayField()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name   = "test.employee",
            Type   = SchemaType.Struct,
            Struct = new StructSchema
            {
                Fields =
                [
                    new StructFieldSchema { Name = "id",   Type = NS_SYSTEM_STRING },
                    new StructFieldSchema { Name = "name", Type = NS_SYSTEM_STRING }
                ]
            }
        });

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name  = "test.employees",
            Type  = SchemaType.Array,
            Array = new ArraySchema { Element = "test.employee", Primary = ["id"] }
        });

        await ctx.SaveAppSchemaAsync(new AppSchema
        {
            Name        = "test.hr",
            ScopePolicy = new AppScopePolicy { Type = AppScopeType.SystemLevel }
        });

        await ctx.SaveAppFieldSchemaAsync("test.hr", new AppFieldSchema
        {
            Name = "staff",
            Type = "test.employees"
        });

        var pushData = new Dictionary<string, AppDataFieldPushQuery>
        {
            ["staff"] = new AppDataFieldPushQuery
            {
                Data = System.Text.Json.Nodes.JsonNode.Parse(
                    """[{"id":"E001","name":"Alice"},{"id":"E002","name":"Bob"}]""")
            }
        };

        (bool pushResult2, _) = await ctx.PushAppDataAsync("test.hr", null, pushData);
        Assert.IsTrue(pushResult2);

        AppType?      app   = await ctx.GetAppTypeAsync("test.hr");
        AppFieldType? field = app!.GetField("staff");

        (AnySchemaNode? records, int total) = await ctx.GetAppFieldDataAsync(field!, AppSchemaDataResult.List);
        Assert.AreEqual(2, total);
        Assert.IsInstanceOfType<ArrayTypeNode>(records);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 12. AppField data – DataCombineType
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A field with DataCombineType.Sum correctly reflects the combine setting
    /// </summary>
    [TestMethod]
    public async Task AppFieldSchema_DataCombine_IsPreserved()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveAppSchemaAsync(new AppSchema
        {
            Name        = "test.stats",
            ScopePolicy = new AppScopePolicy { Type = AppScopeType.SystemLevel }
        });

        await ctx.SaveAppFieldSchemaAsync("test.stats", new AppFieldSchema
        {
            Name    = "total",
            Type    = NS_SYSTEM_INT,
            Combine = DataCombineType.Sum
        });

        AppType?      app   = await ctx.GetAppTypeAsync("test.stats");
        AppFieldType? field = app?.GetField("total");

        Assert.IsNotNull(field);
        Assert.AreEqual(DataCombineType.Sum, field.Combine);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 13. AppSchema – save with inline fields
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// SaveAppSchemaAsync with Fields inline creates the fields in a single call
    /// </summary>
    [TestMethod]
    public async Task AppSchema_SaveWithInlineFields()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        bool saved = await ctx.SaveAppSchemaAsync(new AppSchema
        {
            Name        = "test.inline",
            ScopePolicy = new AppScopePolicy { Type = AppScopeType.SystemLevel },
            Fields      =
            [
                new AppFieldSchema { Name = "x", Type = NS_SYSTEM_INT,    Seqno = 1 },
                new AppFieldSchema { Name = "y", Type = NS_SYSTEM_STRING, Seqno = 2 }
            ]
        });

        Assert.IsTrue(saved);

        AppType? app = await ctx.GetAppTypeAsync("test.inline");
        Assert.IsNotNull(app);
        Assert.IsTrue(app.Fields?.Any(f => f.Name == "x") == true);
        Assert.IsTrue(app.Fields?.Any(f => f.Name == "y") == true);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 14. AppType – IsUsed property
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// IsUsed is false for an empty app, true after a field is added
    /// </summary>
    [TestMethod]
    public async Task AppType_IsUsed_ReflectsFieldPresence()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveAppSchemaAsync(new AppSchema
        {
            Name        = "test.empty",
            ScopePolicy = new AppScopePolicy { Type = AppScopeType.SystemLevel }
        });

        AppType? empty = await ctx.GetAppTypeAsync("test.empty");
        Assert.IsNotNull(empty);
        Assert.IsFalse(empty.IsUsed, "App with no fields should not be IsUsed");

        await ctx.SaveAppFieldSchemaAsync("test.empty", new AppFieldSchema
        {
            Name = "val",
            Type = NS_SYSTEM_BOOL
        });

        AppType? withField = await ctx.GetAppTypeAsync("test.empty");
        Assert.IsTrue(withField?.IsUsed == true, "App with a field should be IsUsed");
    }

    // ─────────────────────────────────────────────────────────────────────
    // 15. Push function – compilation and field wiring
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// After loading an app whose target field declares Func/Arg, verify that
    /// PushFuncSchema is compiled, PushSource is wired, and the source field
    /// gains the target as an observer.
    /// </summary>
    [TestMethod]
    public async Task PushFunction_FieldSetup_IsCompiledAndWired()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();
        await SetupSimplePushSchema(ctx, "test.pushwire");

        AppType?      app    = await ctx.GetAppTypeAsync("test.pushwire");
        AppFieldType? src    = app!.GetField("entries");
        AppFieldType? target = app!.GetField("stats");

        Assert.IsNotNull(target!.PushFuncSchema,  "PushFuncSchema should be compiled");
        Assert.IsNotNull(target!.PushSource,       "PushSource should point to source field");
        Assert.AreEqual("entries", target.PushSource!.Name);
        Assert.IsTrue(src!.HasObserver,            "Source field should have the target as observer");
        Assert.AreEqual(SchemaNodeStatus.Ready, target.Status ?? SchemaNodeStatus.Ready);
    }

    /// <summary>
    /// Saving a field with a Func that has more than one argument is rejected
    /// with ApplicationFieldWrongFunc status.
    /// </summary>
    [TestMethod]
    public async Task PushFunction_WrongArgCount_StatusIsError()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        // binary function (two args) – not valid as a push function
        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.pushbad.fn2",
            Type = SchemaType.Func,
            Func = new FunctionSchema
            {
                Return = NS_SYSTEM_INT,
                Args   =
                [
                    new FuncArg { Name = "a", Type = NS_SYSTEM_INT },
                    new FuncArg { Name = "b", Type = NS_SYSTEM_INT }
                ],
                Exps   =
                [
                    new FuncExp
                    {
                        Name   = "result",
                        Func   = "system.math.add",
                        Return = NS_SYSTEM_INT,
                        Args   = [new FuncCallArg { Name = "a" }, new FuncCallArg { Name = "b" }]
                    }
                ]
            }
        });

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name   = "test.pushbad.elem",
            Type   = SchemaType.Struct,
            Struct = new StructSchema
            {
                Fields = [new StructFieldSchema { Name = "id", Type = NS_SYSTEM_STRING }]
            }
        });

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name  = "test.pushbad.elems",
            Type  = SchemaType.Array,
            Array = new ArraySchema { Element = "test.pushbad.elem", Primary = ["id"] }
        });

        await ctx.SaveAppSchemaAsync(new AppSchema
        {
            Name        = "test.pushbad",
            ScopePolicy = new AppScopePolicy { Type = AppScopeType.SystemLevel }
        });

        await ctx.SaveAppFieldSchemaAsync("test.pushbad", new AppFieldSchema
        {
            Name = "src",
            Type = "test.pushbad.elems"
        });

        await ctx.SaveAppFieldSchemaAsync("test.pushbad", new AppFieldSchema
        {
            Name = "tgt",
            Type = NS_SYSTEM_INT,
            Func = "test.pushbad.fn2",
            Arg  = "src"
        });

        AppType?      app = await ctx.GetAppTypeAsync("test.pushbad");
        AppFieldType? tgt = app?.GetField("tgt");

        Assert.IsNotNull(tgt);
        Assert.AreEqual(SchemaNodeStatus.ApplicationFieldWrongFunc, tgt.Status,
            "Push function with wrong arg count should result in error status");
    }

    // ─────────────────────────────────────────────────────────────────────
    // 16. Push function – end-to-end data propagation
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Inserting records into a source field triggers the push function and
    /// writes the mapped results into the target field on CommitTransactionAsync.
    /// </summary>
    [TestMethod]
    public async Task DataPush_DirectMapping_TargetPopulatedOnCommit()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();
        await SetupSimplePushSchema(ctx, "test.pushdirect");

        ctx.SetAccess("test.pushdirect");

        AppType?      app    = await ctx.GetAppTypeAsync("test.pushdirect");
        AppFieldType? srcFld = app!.GetField("entries");
        AppFieldType? tgtFld = app!.GetField("stats");

        ArrayType  srcArr    = (ArrayType)srcFld!.SchemaType!;
        StructType srcStruct = (StructType)srcArr.ElementSchemaType!;

        StructTypeNode r1 = (StructTypeNode)srcStruct.CreateNode()!;
        r1["id"]  = "g1";
        r1["val"] = 10;

        StructTypeNode r2 = (StructTypeNode)srcStruct.CreateNode()!;
        r2["id"]  = "g2";
        r2["val"] = 25;

        await ctx.BeginTransactionAsync();
        await ctx.SaveFieldDataAsync(srcFld, new ArrayTypeNode(srcArr, new AnySchemaNode[] { r1, r2 }));
        await ctx.CommitTransactionAsync();

        (AnySchemaNode? result, int total) = await ctx.GetAppFieldDataAsync(tgtFld!, AppSchemaDataResult.List);

        Assert.AreEqual(2, total, "Two source records should produce two target records");
        ArrayTypeNode arr = (ArrayTypeNode)result!;

        StructTypeNode g1 = arr.Cast<StructTypeNode>().First(s => s.GetField("gid")?.ToValue<string>() == "g1");
        StructTypeNode g2 = arr.Cast<StructTypeNode>().First(s => s.GetField("gid")?.ToValue<string>() == "g2");

        Assert.AreEqual(10L,  g1.GetField("total")?.ToValue<long>());
        Assert.AreEqual(25L,  g2.GetField("total")?.ToValue<long>());
    }

    /// <summary>
    /// Updating an existing source record causes the push to apply an incremental
    /// delta (Sum combine) to the target field rather than a full overwrite.
    /// </summary>
    [TestMethod]
    public async Task DataPush_IncrementalUpdate_SumCombineApplied()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();
        await SetupSimplePushSchema(ctx, "test.pushincr");

        ctx.SetAccess("test.pushincr");

        AppType?      app    = await ctx.GetAppTypeAsync("test.pushincr");
        AppFieldType? srcFld = app!.GetField("entries");
        AppFieldType? tgtFld = app!.GetField("stats");

        ArrayType  srcArr    = (ArrayType)srcFld!.SchemaType!;
        StructType srcStruct = (StructType)srcArr.ElementSchemaType!;

        // First transaction: insert {id:"u1", val:100}
        StructTypeNode first = (StructTypeNode)srcStruct.CreateNode()!;
        first["id"]  = "u1";
        first["val"] = 100;

        await ctx.BeginTransactionAsync();
        await ctx.SaveFieldDataAsync(srcFld, new ArrayTypeNode(srcArr, new AnySchemaNode[] { first }));
        await ctx.CommitTransactionAsync();

        // Second transaction: update same key with val=160
        StructTypeNode updated = (StructTypeNode)srcStruct.CreateNode()!;
        updated["id"]  = "u1";
        updated["val"] = 160;

        await ctx.BeginTransactionAsync();
        await ctx.SaveFieldDataAsync(srcFld, new ArrayTypeNode(srcArr, new AnySchemaNode[] { updated }));
        await ctx.CommitTransactionAsync();

        (AnySchemaNode? result, _) = await ctx.GetAppFieldDataAsync(tgtFld!, AppSchemaDataResult.List);
        ArrayTypeNode arr = (ArrayTypeNode)result!;

        StructTypeNode u1 = arr.Cast<StructTypeNode>().First(s => s.GetField("gid")?.ToValue<string>() == "u1");
        Assert.AreEqual(160L, u1.GetField("total")?.ToValue<long>(),
            "After incremental update the target total should reflect the new source value");
    }

    /// <summary>
    /// When multiple source records share the same target primary key, the push
    /// function accumulates (Sum combine) all their contributions into one row.
    /// </summary>
    [TestMethod]
    public async Task DataPush_MultipleSourcesSameKey_SumAccumulates()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        // Custom schema: entries keyed by {group, memberId}, stats keyed by {group}
        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name   = "test.pushmultientry",
            Type   = SchemaType.Struct,
            Struct = new StructSchema
            {
                Fields =
                [
                    new StructFieldSchema { Name = "group",    Type = NS_SYSTEM_STRING },
                    new StructFieldSchema { Name = "memberid", Type = NS_SYSTEM_STRING },
                    new StructFieldSchema { Name = "score",    Type = NS_SYSTEM_INT }
                ]
            }
        });

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name  = "test.pushmultientries",
            Type  = SchemaType.Array,
            Array = new ArraySchema { Element = "test.pushmultientry", Primary = ["group", "memberid"] }
        });

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name   = "test.pushmultistat",
            Type   = SchemaType.Struct,
            Struct = new StructSchema
            {
                Fields =
                [
                    new StructFieldSchema { Name = "group", Type = NS_SYSTEM_STRING },
                    new StructFieldSchema { Name = "total", Type = NS_SYSTEM_INT }
                ]
            }
        });

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name  = "test.pushmultistats",
            Type  = SchemaType.Array,
            Array = new ArraySchema { Element = "test.pushmultistat", Primary = ["group"] }
        });

        // Push function: maps each entry to {group, total=score}
        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.pushmultifn",
            Type = SchemaType.Func,
            Func = new FunctionSchema
            {
                Return = "test.pushmultistat",
                Args   = [new FuncArg { Name = "item", Type = "test.pushmultientry" }],
                Exps   =
                [
                    new FuncExp
                    {
                        Name   = "group",
                        Func   = "system.collection.getfield",
                        Return = NS_SYSTEM_STRING,
                        Args   =
                        [
                            new FuncCallArg { Name  = "item" },
                            new FuncCallArg { Value = System.Text.Json.Nodes.JsonValue.Create("group") },
                            new FuncCallArg { Value = System.Text.Json.Nodes.JsonValue.Create("_") }
                        ]
                    },
                    new FuncExp
                    {
                        Name   = "total",
                        Func   = "system.collection.getfield",
                        Return = NS_SYSTEM_INT,
                        Args   =
                        [
                            new FuncCallArg { Name  = "item" },
                            new FuncCallArg { Value = System.Text.Json.Nodes.JsonValue.Create("score") },
                            new FuncCallArg { Value = System.Text.Json.Nodes.JsonValue.Create(0L) }
                        ]
                    }
                ]
            }
        });

        await ctx.SaveAppSchemaAsync(new AppSchema
        {
            Name        = "test.pushmulti",
            ScopePolicy = new AppScopePolicy { Type = AppScopeType.SystemLevel }
        });

        await ctx.SaveAppFieldSchemaAsync("test.pushmulti", new AppFieldSchema
        {
            Name = "entries",
            Type = "test.pushmultientries"
        });

        await ctx.SaveAppFieldSchemaAsync("test.pushmulti", new AppFieldSchema
        {
            Name = "stats",
            Type = "test.pushmultistats",
            Func = "test.pushmultifn",
            Arg  = "entries"
        });

        ctx.SetAccess("test.pushmulti");

        AppType?      app    = await ctx.GetAppTypeAsync("test.pushmulti");
        AppFieldType? srcFld = app!.GetField("entries");
        AppFieldType? tgtFld = app!.GetField("stats");

        ArrayType  srcArr    = (ArrayType)srcFld!.SchemaType!;
        StructType srcStruct = (StructType)srcArr.ElementSchemaType!;

        // Three members all in group "A"
        StructTypeNode m1 = BuildEntry(srcStruct, "A", "m1", 10);
        StructTypeNode m2 = BuildEntry(srcStruct, "A", "m2", 20);
        StructTypeNode m3 = BuildEntry(srcStruct, "A", "m3", 30);

        await ctx.BeginTransactionAsync();
        await ctx.SaveFieldDataAsync(srcFld, new ArrayTypeNode(srcArr, new AnySchemaNode[] { m1, m2, m3 }));
        await ctx.CommitTransactionAsync();

        (AnySchemaNode? result, int total) = await ctx.GetAppFieldDataAsync(tgtFld!, AppSchemaDataResult.List);
        Assert.AreEqual(1, total, "All members in group A should collapse to a single stat row");

        StructTypeNode groupA = ((ArrayTypeNode)result!).Cast<StructTypeNode>().First();
        Assert.AreEqual("A",  groupA.GetField("group")?.ToValue<string>());
        Assert.AreEqual(60L,  groupA.GetField("total")?.ToValue<long>(),
            "Total should be 10 + 20 + 30 = 60");

        static StructTypeNode BuildEntry(StructType t, string group, string memberId, int score)
        {
            var n = (StructTypeNode)t.CreateNode()!;
            n["group"]    = group;
            n["memberid"] = memberId;
            n["score"]    = score;
            return n;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // 17. Push function – cascade push (A → B → C observer chain)
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A push field that is itself observed by another push field forms an
    /// observer chain.  Verify that the intermediate field has both a source
    /// (it is a push target) and observers (it feeds a second push target).
    /// </summary>
    [TestMethod]
    public async Task DataPush_CascadeChain_IntermediateFieldHasObserver()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();
        await SetupCascadePushSchema(ctx, "test.pushcascadewire");

        AppType?      app    = await ctx.GetAppTypeAsync("test.pushcascadewire");
        AppFieldType? srcFld = app!.GetField("rawscores");
        AppFieldType? midFld = app!.GetField("groupscores");
        AppFieldType? dstFld = app!.GetField("totals");

        Assert.IsNotNull(midFld!.PushSource,  "groupscores should have a push source");
        Assert.IsTrue(srcFld!.HasObserver,    "rawscores should observe groupscores");
        Assert.IsTrue(midFld.HasObserver,     "groupscores should observe totals (cascade)");
        Assert.IsNotNull(dstFld!.PushSource,  "totals should have a push source");
    }

    /// <summary>
    /// Two-level cascade push A→B→C: committing source changes propagates
    /// through the intermediate field and accumulates in the final target.
    /// </summary>
    [TestMethod]
    public async Task DataPush_CascadeChain_EndToEnd()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();
        await SetupCascadePushSchema(ctx, "test.pushcascadee2e");

        ctx.SetAccess("test.pushcascadee2e");

        AppType?      app    = await ctx.GetAppTypeAsync("test.pushcascadee2e");
        AppFieldType? srcFld = app!.GetField("rawscores");
        AppFieldType? midFld = app!.GetField("groupscores");
        AppFieldType? dstFld = app!.GetField("totals");

        ArrayType  srcArr    = (ArrayType)srcFld!.SchemaType!;
        StructType srcStruct = (StructType)srcArr.ElementSchemaType!;

        // Insert three raw scores for two groups
        StructTypeNode r1 = BuildRaw(srcStruct, "A", "m1", 10);
        StructTypeNode r2 = BuildRaw(srcStruct, "A", "m2", 20);
        StructTypeNode r3 = BuildRaw(srcStruct, "B", "m3", 5);

        await ctx.BeginTransactionAsync();
        await ctx.SaveFieldDataAsync(srcFld, new ArrayTypeNode(srcArr, new AnySchemaNode[] { r1, r2, r3 }));
        await ctx.CommitTransactionAsync();

        // Level 2: groupscores → {grp, pts}
        (AnySchemaNode? midResult, int midCount) = await ctx.GetAppFieldDataAsync(midFld!, AppSchemaDataResult.List);
        Assert.AreEqual(2, midCount, "Two distinct groups should produce two groupscore records");

        ArrayTypeNode midArr = (ArrayTypeNode)midResult!;
        StructTypeNode grpA = midArr.Cast<StructTypeNode>().First(s => s.GetField("grp")?.ToValue<string>() == "A");
        StructTypeNode grpB = midArr.Cast<StructTypeNode>().First(s => s.GetField("grp")?.ToValue<string>() == "B");
        Assert.AreEqual(30L, grpA.GetField("pts")?.ToValue<long>(), "Group A: 10 + 20 = 30");
        Assert.AreEqual(5L,  grpB.GetField("pts")?.ToValue<long>(), "Group B: 5");

        // Level 3: totals mirrors groupscores (id=grp, grand=pts) – one row per group
        (AnySchemaNode? dstResult, int dstCount) = await ctx.GetAppFieldDataAsync(dstFld!, AppSchemaDataResult.List);
        Assert.AreEqual(2, dstCount, "totals should have one row per group");
        ArrayTypeNode dstArr = (ArrayTypeNode)dstResult!;

        StructTypeNode tA = dstArr.Cast<StructTypeNode>().First(s => s.GetField("id")?.ToValue<string>() == "A");
        StructTypeNode tB = dstArr.Cast<StructTypeNode>().First(s => s.GetField("id")?.ToValue<string>() == "B");
        Assert.AreEqual(30L, tA.GetField("grand")?.ToValue<long>(), "totals[A].grand = 30");
        Assert.AreEqual(5L,  tB.GetField("grand")?.ToValue<long>(), "totals[B].grand = 5");

        static StructTypeNode BuildRaw(StructType t, string grp, string memberId, int score)
        {
            var n = (StructTypeNode)t.CreateNode()!;
            n["grp"]      = grp;
            n["memberid"] = memberId;
            n["score"]    = score;
            return n;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Helpers – shared schema setup
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a simple push schema:
    ///   entries  (source) : Array&lt;{id:string, val:int}&gt; primary=[id]
    ///   stats    (target) : Array&lt;{gid:string, total:int}&gt; primary=[gid]
    ///   push fn          : entry → stat  (gid=id, total=val, Sum on total)
    /// </summary>
    private static async Task SetupSimplePushSchema(SchemaContext ctx, string appName)
    {
        // Flatten the app name into a single-segment namespace for type names
        // e.g. "test.push.direct" → "testpushdirect"
        string ns = appName.Replace(".", "");

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name   = $"{ns}.entry",
            Type   = SchemaType.Struct,
            Struct = new StructSchema
            {
                Fields =
                [
                    new StructFieldSchema { Name = "id",  Type = NS_SYSTEM_STRING },
                    new StructFieldSchema { Name = "val", Type = NS_SYSTEM_INT }
                ]
            }
        });

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name  = $"{ns}.entries",
            Type  = SchemaType.Array,
            Array = new ArraySchema { Element = $"{ns}.entry", Primary = ["id"] }
        });

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name   = $"{ns}.stat",
            Type   = SchemaType.Struct,
            Struct = new StructSchema
            {
                Fields =
                [
                    new StructFieldSchema { Name = "gid",   Type = NS_SYSTEM_STRING },
                    new StructFieldSchema { Name = "total", Type = NS_SYSTEM_INT }
                ]
            }
        });

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name  = $"{ns}.stats",
            Type  = SchemaType.Array,
            Array = new ArraySchema { Element = $"{ns}.stat", Primary = ["gid"] }
        });

        // Push function: entry → stat  (gid = entry.id, total = entry.val)
        // Use system.collection.getfield with a non-empty default so the FieldAccessExp
        // compilation has all 4 required arguments (context, obj, field, @default).
        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = $"{ns}.fn",
            Type = SchemaType.Func,
            Func = new FunctionSchema
            {
                Return = $"{ns}.stat",
                Args   = [new FuncArg { Name = "item", Type = $"{ns}.entry" }],
                Exps   =
                [
                    new FuncExp
                    {
                        Name   = "gid",
                        Func   = "system.collection.getfield",
                        Return = NS_SYSTEM_STRING,
                        Args   =
                        [
                            new FuncCallArg { Name  = "item" },
                            new FuncCallArg { Value = System.Text.Json.Nodes.JsonValue.Create("id") },
                            new FuncCallArg { Value = System.Text.Json.Nodes.JsonValue.Create("_") }
                        ]
                    },
                    new FuncExp
                    {
                        Name   = "total",
                        Func   = "system.collection.getfield",
                        Return = NS_SYSTEM_INT,
                        Args   =
                        [
                            new FuncCallArg { Name  = "item" },
                            new FuncCallArg { Value = System.Text.Json.Nodes.JsonValue.Create("val") },
                            new FuncCallArg { Value = System.Text.Json.Nodes.JsonValue.Create(0L) }
                        ]
                    }
                ]
            }
        });

        await ctx.SaveAppSchemaAsync(new AppSchema
        {
            Name        = appName,
            ScopePolicy = new AppScopePolicy { Type = AppScopeType.SystemLevel }
        });

        await ctx.SaveAppFieldSchemaAsync(appName, new AppFieldSchema
        {
            Name = "entries",
            Type = $"{ns}.entries"
        });

        await ctx.SaveAppFieldSchemaAsync(appName, new AppFieldSchema
        {
            Name = "stats",
            Type = $"{ns}.stats",
            Func = $"{ns}.fn",
            Arg  = "entries"
        });
    }

    /// <summary>
    /// Builds a two-level cascade push schema A→B→C:
    ///   rawscores   (A, source) : Array&lt;{grp, memberId, score}&gt; primary=[grp,memberId]
    ///   groupscores (B, mid)    : Array&lt;{grp, pts}&gt;             primary=[grp]  ← pushed from A
    ///   totals      (C, dest)   : Array&lt;{id, grand}&gt;            primary=[id]   ← pushed from B
    /// </summary>
    private static async Task SetupCascadePushSchema(SchemaContext ctx, string appName)
    {
        string ns = appName.Replace(".", "");

        // ---- Level A: raw score records ----
        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name   = $"{ns}.rawscore",
            Type   = SchemaType.Struct,
            Struct = new StructSchema
            {
                Fields =
                [
                    new StructFieldSchema { Name = "grp",      Type = NS_SYSTEM_STRING },
                    new StructFieldSchema { Name = "memberid", Type = NS_SYSTEM_STRING },
                    new StructFieldSchema { Name = "score",    Type = NS_SYSTEM_INT }
                ]
            }
        });

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name  = $"{ns}.rawscores",
            Type  = SchemaType.Array,
            Array = new ArraySchema { Element = $"{ns}.rawscore", Primary = ["grp", "memberid"] }
        });

        // ---- Level B: per-group score totals ----
        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name   = $"{ns}.grpscore",
            Type   = SchemaType.Struct,
            Struct = new StructSchema
            {
                Fields =
                [
                    new StructFieldSchema { Name = "grp", Type = NS_SYSTEM_STRING },
                    new StructFieldSchema { Name = "pts", Type = NS_SYSTEM_INT }
                ]
            }
        });

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name  = $"{ns}.groupscores",
            Type  = SchemaType.Array,
            Array = new ArraySchema { Element = $"{ns}.grpscore", Primary = ["grp"] }
        });

        // A→B push function: rawscore → grpscore (grp=grp, pts=score)
        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = $"{ns}.fn1",
            Type = SchemaType.Func,
            Func = new FunctionSchema
            {
                Return = $"{ns}.grpscore",
                Args   = [new FuncArg { Name = "item", Type = $"{ns}.rawscore" }],
                Exps   =
                [
                    new FuncExp
                    {
                        Name   = "grp",
                        Func   = "system.collection.getfield",
                        Return = NS_SYSTEM_STRING,
                        Args   =
                        [
                            new FuncCallArg { Name  = "item" },
                            new FuncCallArg { Value = System.Text.Json.Nodes.JsonValue.Create("grp") },
                            new FuncCallArg { Value = System.Text.Json.Nodes.JsonValue.Create("_") }
                        ]
                    },
                    new FuncExp
                    {
                        Name   = "pts",
                        Func   = "system.collection.getfield",
                        Return = NS_SYSTEM_INT,
                        Args   =
                        [
                            new FuncCallArg { Name  = "item" },
                            new FuncCallArg { Value = System.Text.Json.Nodes.JsonValue.Create("score") },
                            new FuncCallArg { Value = System.Text.Json.Nodes.JsonValue.Create(0L) }
                        ]
                    }
                ]
            }
        });

        // ---- Level C: grand total across all groups ----
        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name   = $"{ns}.total",
            Type   = SchemaType.Struct,
            Struct = new StructSchema
            {
                Fields =
                [
                    new StructFieldSchema { Name = "id",    Type = NS_SYSTEM_STRING },
                    new StructFieldSchema { Name = "grand", Type = NS_SYSTEM_INT }
                ]
            }
        });

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name  = $"{ns}.totals",
            Type  = SchemaType.Array,
            Array = new ArraySchema { Element = $"{ns}.total", Primary = ["id"] }
        });

        // B→C push function: grpscore → total (id=grp, grand=pts)
        // Each group score maps to a totals row keyed by the same group name.
        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = $"{ns}.fn2",
            Type = SchemaType.Func,
            Func = new FunctionSchema
            {
                Return = $"{ns}.total",
                Args   = [new FuncArg { Name = "item", Type = $"{ns}.grpscore" }],
                Exps   =
                [
                    new FuncExp
                    {
                        Name   = "id",
                        Func   = "system.collection.getfield",
                        Return = NS_SYSTEM_STRING,
                        Args   =
                        [
                            new FuncCallArg { Name  = "item" },
                            new FuncCallArg { Value = System.Text.Json.Nodes.JsonValue.Create("grp") },
                            new FuncCallArg { Value = System.Text.Json.Nodes.JsonValue.Create("_") }
                        ]
                    },
                    new FuncExp
                    {
                        Name   = "grand",
                        Func   = "system.collection.getfield",
                        Return = NS_SYSTEM_INT,
                        Args   =
                        [
                            new FuncCallArg { Name  = "item" },
                            new FuncCallArg { Value = System.Text.Json.Nodes.JsonValue.Create("pts") },
                            new FuncCallArg { Value = System.Text.Json.Nodes.JsonValue.Create(0L) }
                        ]
                    }
                ]
            }
        });

        // ---- App and fields ----
        await ctx.SaveAppSchemaAsync(new AppSchema
        {
            Name        = appName,
            ScopePolicy = new AppScopePolicy { Type = AppScopeType.SystemLevel }
        });

        await ctx.SaveAppFieldSchemaAsync(appName, new AppFieldSchema
        {
            Name = "rawscores",
            Type = $"{ns}.rawscores"
        });

        await ctx.SaveAppFieldSchemaAsync(appName, new AppFieldSchema
        {
            Name = "groupscores",
            Type = $"{ns}.groupscores",
            Func = $"{ns}.fn1",
            Arg  = "rawscores"
        });

        await ctx.SaveAppFieldSchemaAsync(appName, new AppFieldSchema
        {
            Name = "totals",
            Type = $"{ns}.totals",
            Func = $"{ns}.fn2",
            Arg  = "groupscores"
        });
    }
}
