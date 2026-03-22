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
/// Tests for App data CRUD, batch query, error handling, target isolation, ClearAll, Exist, and scalar field storage
/// </summary>
[TestClass]
public class AppDataTest : TestBase
{
    // ─────────────────────────────────────────────────────────────────────
    // App data CRUD (persistence using InMemoryAppDataProvider)
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Full flow: define type → app → write → read and verify (reproduces the original sample)
    /// </summary>
    [TestMethod]
    public async Task ComplexAppTest()
    {
        var schemaContext = ServiceProvider.GetRequiredService<SchemaContext>();

        // 1. Define a simple scalar type
        await schemaContext.SaveSchemaAsync(new NodeSchema
        {
            Name = "test",
            Type = SchemaType.Namespace,
            Schemas =
            [
                new NodeSchema
                {
                    Name = "test.enum",
                    Type = SchemaType.Enum,
                    Enum = new EnumSchema
                    {
                        Type = EnumValueType.Int,
                        Values =
                        [
                            new EnumValueInfo { Value = "1", Name = "Value1" },
                            new EnumValueInfo { Value = "2", Name = "Value2" }
                        ]
                    }
                },
                new NodeSchema
                {
                    Name   = "test.struct",
                    Type   = SchemaType.Struct,
                    Struct = new StructSchema
                    {
                        Fields =
                        [
                            new StructFieldSchema { Name = "type", Type = "test.enum" },
                            new StructFieldSchema { Name = "name", Type = NS_SYSTEM_STRING },
                            new StructFieldSchema { Name = "age",  Type = NS_SYSTEM_INT, LowLimit = "0" }
                        ]
                    }
                },
                new NodeSchema
                {
                    Name  = "test.array",
                    Type  = SchemaType.Array,
                    Array = new ArraySchema { Element = "test.struct", Primary = ["name"] }
                }
            ]
        });

        // 2. Define an AppType to store the data
        await schemaContext.SaveAppSchemaAsync(new AppSchema { Name = "test" });
        await schemaContext.SaveAppFieldSchemaAsync("test", new AppFieldSchema { Name = "value", Type = "test.array" });

        // 3. Write data
        await schemaContext.PushAppDataAsync("test", Guid.Empty.ToString(), new Dictionary<string, AppDataFieldPushQuery>
        {
            ["value"] = new AppDataFieldPushQuery
            {
                Data = new JsonArray
                {
                    new JsonObject { ["type"] = 1, ["name"] = "Alice", ["age"] = JsonValue.Create(30) },
                    new JsonObject { ["type"] = 2, ["name"] = "Bob",   ["age"] = JsonValue.Create(25) }
                }
            }
        });

        // 4. Read the data back and verify
        ArrayTypeNode? res = (await schemaContext.GetSchemaDataAsync("test", "value", Guid.Empty.ToString(), AppSchemaDataResult.List)) as ArrayTypeNode;
        Assert.IsNotNull(res);
        Assert.AreEqual(2, res.Count);
    }

    /// <summary>
    /// SystemLevel app: no target required, data is globally shared
    /// </summary>
    [TestMethod]
    public async Task AppData_SystemLevel_NoTarget()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name   = "test.cfg",
            Type   = SchemaType.Struct,
            Struct = new StructSchema
            {
                Fields = [new StructFieldSchema { Name = "key", Type = NS_SYSTEM_STRING }]
            }
        });

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name  = "test.cfgs",
            Type  = SchemaType.Array,
            Array = new ArraySchema { Element = "test.cfg", Primary = ["key"] }
        });

        await ctx.SaveAppSchemaAsync(new AppSchema
        {
            Name        = "sysconfig",
            ScopePolicy = new AppScopePolicy { Type = AppScopeType.SystemLevel }
        });

        await ctx.SaveAppFieldSchemaAsync("sysconfig", new AppFieldSchema
        {
            Name = "settings",
            Type = "test.cfgs"
        });

        await ctx.PushAppDataAsync("sysconfig", null, new Dictionary<string, AppDataFieldPushQuery>
        {
            ["settings"] = new AppDataFieldPushQuery
            {
                Data = new JsonArray
                {
                    new JsonObject { ["key"] = "theme" },
                    new JsonObject { ["key"] = "locale" }
                }
            }
        });

        var res = (await ctx.GetSchemaDataAsync("sysconfig", "settings", null, AppSchemaDataResult.List)) as ArrayTypeNode;
        Assert.IsNotNull(res);
        Assert.AreEqual(2, res.Count);
    }

    /// <summary>
    /// Update data: update an existing entry matched by primary key
    /// </summary>
    [TestMethod]
    public async Task AppData_Update_ExistingRecord()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

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

        await ctx.SaveAppSchemaAsync(new AppSchema { Name = "shop" });
        await ctx.SaveAppFieldSchemaAsync("shop", new AppFieldSchema { Name = "inventory", Type = "test.products" });

        var target = "store-1";

        // initial write
        await ctx.PushAppDataAsync("shop", target, new Dictionary<string, AppDataFieldPushQuery>
        {
            ["inventory"] = new AppDataFieldPushQuery
            {
                Data = new JsonArray
                {
                    new JsonObject { ["sku"] = "ABC", ["price"] = JsonValue.Create(100) }
                }
            }
        });

        // update price
        await ctx.PushAppDataAsync("shop", target, new Dictionary<string, AppDataFieldPushQuery>
        {
            ["inventory"] = new AppDataFieldPushQuery
            {
                Data = new JsonArray
                {
                    new JsonObject { ["sku"] = "ABC", ["price"] = JsonValue.Create(150) }
                }
            }
        });

        var res = (await ctx.GetSchemaDataAsync("shop", "inventory", target, AppSchemaDataResult.List)) as ArrayTypeNode;
        Assert.IsNotNull(res);
        Assert.AreEqual(1, res.Count);

        var item = res[0] as StructTypeNode;
        Assert.IsNotNull(item);
        Assert.AreEqual(150L, item.GetField("price")?.ToValue<long>());
    }

    /// <summary>
    /// Delete a data record and verify the remaining count is correct
    /// </summary>
    [TestMethod]
    public async Task AppData_Delete_Record()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name   = "test.task",
            Type   = SchemaType.Struct,
            Struct = new StructSchema
            {
                Fields =
                [
                    new StructFieldSchema { Name = "id",    Type = NS_SYSTEM_STRING },
                    new StructFieldSchema { Name = "title", Type = NS_SYSTEM_STRING }
                ]
            }
        });

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name  = "test.tasks",
            Type  = SchemaType.Array,
            Array = new ArraySchema { Element = "test.task", Primary = ["id"] }
        });

        await ctx.SaveAppSchemaAsync(new AppSchema { Name = "todos" });
        await ctx.SaveAppFieldSchemaAsync("todos", new AppFieldSchema { Name = "items", Type = "test.tasks" });

        var target = "user-1";

        await ctx.PushAppDataAsync("todos", target, new Dictionary<string, AppDataFieldPushQuery>
        {
            ["items"] = new AppDataFieldPushQuery
            {
                Data = new JsonArray
                {
                    new JsonObject { ["id"] = "1", ["title"] = "Buy milk" },
                    new JsonObject { ["id"] = "2", ["title"] = "Read book" }
                }
            }
        });

        // delete id=1
        await ctx.PushAppDataAsync("todos", target, new Dictionary<string, AppDataFieldPushQuery>
        {
            ["items"] = new AppDataFieldPushQuery
            {
                Deletes = [new JsonObject { ["id"] = "1" }]
            }
        });

        var res = (await ctx.GetSchemaDataAsync("todos", "items", target, AppSchemaDataResult.List)) as ArrayTypeNode;
        Assert.IsNotNull(res);
        Assert.AreEqual(1, res.Count);

        var remaining = res[0] as StructTypeNode;
        Assert.IsNotNull(remaining);
        Assert.AreEqual("2", remaining.GetField("id")?.ToValue<string>());
    }

    /// <summary>
    /// AppSchemaDataResult.Count returns the correct count
    /// </summary>
    [TestMethod]
    public async Task AppData_Query_Count()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name   = "test.note",
            Type   = SchemaType.Struct,
            Struct = new StructSchema
            {
                Fields = [new StructFieldSchema { Name = "nid", Type = NS_SYSTEM_STRING }]
            }
        });

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name  = "test.notes",
            Type  = SchemaType.Array,
            Array = new ArraySchema { Element = "test.note", Primary = ["nid"] }
        });

        await ctx.SaveAppSchemaAsync(new AppSchema { Name = "notebook" });
        await ctx.SaveAppFieldSchemaAsync("notebook", new AppFieldSchema { Name = "entries", Type = "test.notes" });

        var target = "owner-1";

        await ctx.PushAppDataAsync("notebook", target, new Dictionary<string, AppDataFieldPushQuery>
        {
            ["entries"] = new AppDataFieldPushQuery
            {
                Data = new JsonArray
                {
                    new JsonObject { ["nid"] = "n1" },
                    new JsonObject { ["nid"] = "n2" },
                    new JsonObject { ["nid"] = "n3" }
                }
            }
        });

        var count = await ctx.GetSchemaDataAsync("notebook", "entries", target, AppSchemaDataResult.Count);
        Assert.IsNotNull(count);
        Assert.AreEqual(3L, count.ToValue<long>());
    }

    /// <summary>
    /// AppSchemaDataResult.First / Last returns the correct first and last element
    /// </summary>
    [TestMethod]
    public async Task AppData_Query_FirstLast()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name   = "test.log",
            Type   = SchemaType.Struct,
            Struct = new StructSchema
            {
                Fields =
                [
                    new StructFieldSchema { Name = "seq",     Type = NS_SYSTEM_STRING },
                    new StructFieldSchema { Name = "message", Type = NS_SYSTEM_STRING }
                ]
            }
        });

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name  = "test.logs",
            Type  = SchemaType.Array,
            Array = new ArraySchema { Element = "test.log", Primary = ["seq"] }
        });

        await ctx.SaveAppSchemaAsync(new AppSchema { Name = "logapp" });
        await ctx.SaveAppFieldSchemaAsync("logapp", new AppFieldSchema { Name = "log", Type = "test.logs" });

        var target = "svc-1";

        await ctx.PushAppDataAsync("logapp", target, new Dictionary<string, AppDataFieldPushQuery>
        {
            ["log"] = new AppDataFieldPushQuery
            {
                Data = new JsonArray
                {
                    new JsonObject { ["seq"] = "001", ["message"] = "start" },
                    new JsonObject { ["seq"] = "002", ["message"] = "middle" },
                    new JsonObject { ["seq"] = "003", ["message"] = "end" }
                }
            }
        });

        var first = await ctx.GetSchemaDataAsync("logapp", "log", target, AppSchemaDataResult.First) as StructTypeNode;
        var last  = await ctx.GetSchemaDataAsync("logapp", "log", target, AppSchemaDataResult.Last)  as StructTypeNode;

        Assert.IsNotNull(first);
        Assert.IsNotNull(last);
        Assert.AreEqual("start", first.GetField("message")?.ToValue<string>());
        Assert.AreEqual("end",   last.GetField("message")?.ToValue<string>());
    }

    // ─────────────────────────────────────────────────────────────────────
    // BatchQueryAppData
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// BatchQueryAppData queries multiple fields simultaneously and returns results
    /// </summary>
    [TestMethod]
    public async Task BatchQuery_MultipleFields()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name   = "test.kv",
            Type   = SchemaType.Struct,
            Struct = new StructSchema
            {
                Fields =
                [
                    new StructFieldSchema { Name = "k", Type = NS_SYSTEM_STRING },
                    new StructFieldSchema { Name = "v", Type = NS_SYSTEM_STRING }
                ]
            }
        });

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name  = "test.kvs",
            Type  = SchemaType.Array,
            Array = new ArraySchema { Element = "test.kv", Primary = ["k"] }
        });

        await ctx.SaveAppSchemaAsync(new AppSchema { Name = "kvapp" });
        await ctx.SaveAppFieldSchemaAsync("kvapp", new AppFieldSchema { Name = "pairs", Type = "test.kvs" });

        var target = "batch-t1";

        await ctx.PushAppDataAsync("kvapp", target, new Dictionary<string, AppDataFieldPushQuery>
        {
            ["pairs"] = new AppDataFieldPushQuery
            {
                Data = new JsonArray
                {
                    new JsonObject { ["k"] = "color", ["v"] = "blue" },
                    new JsonObject { ["k"] = "size",  ["v"] = "large" }
                }
            }
        });

        var (results, _) = await ctx.BatchQueryAppDataAsync(
        [
            new AppDataQuery
            {
                App    = "kvapp",
                Target = target,
                Fields = ["pairs"]
            }
        ]);

        Assert.IsNotNull(results);
        Assert.AreEqual(1, results.Length);

        var fieldResult = results[0].Results?.GetValueOrDefault("pairs");
        Assert.IsNotNull(fieldResult);
    }

    // ─────────────────────────────────────────────────────────────────────
    // PushAppData error handling
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns APP_NOT_FOUND when the app name is empty
    /// </summary>
    [TestMethod]
    public async Task PushAppData_EmptyApp_ReturnsNotFound()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        var (result, error) = await ctx.PushAppDataAsync("", "t1", new Dictionary<string, AppDataFieldPushQuery>
        {
            ["f"] = new AppDataFieldPushQuery { Data = JsonValue.Create(1) }
        });

        Assert.IsFalse(result);
        Assert.IsNotNull(error);
        Assert.AreEqual(APP_NOT_FOUND, (error as JsonValue)!.GetValue<string>());
    }

    /// <summary>
    /// Returns APP_PUSH_DATA_REQUIRED when the data dictionary is null
    /// </summary>
    [TestMethod]
    public async Task PushAppData_EmptyData_ReturnsError()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        var (result, error) = await ctx.PushAppDataAsync("anyapp", "t1", null);

        Assert.IsFalse(result);
        Assert.IsNotNull(error);
        Assert.AreEqual(APP_PUSH_DATA_REQUIRED, (error as JsonValue)!.GetValue<string>());
    }

    /// <summary>
    /// Returns APP_TARGET_REQUIRED when target is omitted for a BusinessTarget-scoped app
    /// </summary>
    [TestMethod]
    public async Task PushAppData_MissingTarget_ReturnsTargetRequired()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        // Default scope is BusinessTarget
        await ctx.SaveAppSchemaAsync(new AppSchema { Name = "errtest" });

        var (result, error) = await ctx.PushAppDataAsync("errtest", null, new Dictionary<string, AppDataFieldPushQuery>
        {
            ["anyfield"] = new AppDataFieldPushQuery { Data = JsonValue.Create("value") }
        });

        Assert.IsFalse(result);
        Assert.IsNotNull(error);
        Assert.AreEqual(APP_TARGET_REQUIRED, (error as JsonValue)!.GetValue<string>());
    }

    // ─────────────────────────────────────────────────────────────────────
    // Target data isolation
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Data for different targets is isolated and does not interfere with each other
    /// </summary>
    [TestMethod]
    public async Task AppData_TargetIsolation()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name   = "test.item",
            Type   = SchemaType.Struct,
            Struct = new StructSchema
            {
                Fields = [new StructFieldSchema { Name = "id", Type = NS_SYSTEM_STRING }]
            }
        });

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name  = "test.itemlist",
            Type  = SchemaType.Array,
            Array = new ArraySchema { Element = "test.item", Primary = ["id"] }
        });

        await ctx.SaveAppSchemaAsync(new AppSchema { Name = "isolatedapp" });
        await ctx.SaveAppFieldSchemaAsync("isolatedapp", new AppFieldSchema { Name = "items", Type = "test.itemlist" });

        await ctx.PushAppDataAsync("isolatedapp", "tenant-1", new Dictionary<string, AppDataFieldPushQuery>
        {
            ["items"] = new AppDataFieldPushQuery
            {
                Data = new JsonArray { new JsonObject { ["id"] = "t1-item" } }
            }
        });

        await ctx.PushAppDataAsync("isolatedapp", "tenant-2", new Dictionary<string, AppDataFieldPushQuery>
        {
            ["items"] = new AppDataFieldPushQuery
            {
                Data = new JsonArray { new JsonObject { ["id"] = "t2-item" } }
            }
        });

        var res1 = (await ctx.GetSchemaDataAsync("isolatedapp", "items", "tenant-1", AppSchemaDataResult.List)) as ArrayTypeNode;
        var res2 = (await ctx.GetSchemaDataAsync("isolatedapp", "items", "tenant-2", AppSchemaDataResult.List)) as ArrayTypeNode;

        Assert.IsNotNull(res1);
        Assert.IsNotNull(res2);
        Assert.AreEqual(1, res1.Count);
        Assert.AreEqual(1, res2.Count);
        Assert.AreEqual("t1-item", (res1[0] as StructTypeNode)?.GetField("id")?.ToValue<string>());
        Assert.AreEqual("t2-item", (res2[0] as StructTypeNode)?.GetField("id")?.ToValue<string>());
    }

    /// <summary>
    /// SystemLevel app data is globally shared; querying with any target yields the same result
    /// </summary>
    [TestMethod]
    public async Task AppData_SystemLevel_SharedAcrossTargets()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name   = "test.sysitem",
            Type   = SchemaType.Struct,
            Struct = new StructSchema
            {
                Fields = [new StructFieldSchema { Name = "key", Type = NS_SYSTEM_STRING }]
            }
        });

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name  = "test.sysitems",
            Type  = SchemaType.Array,
            Array = new ArraySchema { Element = "test.sysitem", Primary = ["key"] }
        });

        await ctx.SaveAppSchemaAsync(new AppSchema
        {
            Name        = "sysshared",
            ScopePolicy = new AppScopePolicy { Type = AppScopeType.SystemLevel }
        });

        await ctx.SaveAppFieldSchemaAsync("sysshared", new AppFieldSchema { Name = "data", Type = "test.sysitems" });

        // Push with no target (SystemLevel)
        await ctx.PushAppDataAsync("sysshared", null, new Dictionary<string, AppDataFieldPushQuery>
        {
            ["data"] = new AppDataFieldPushQuery
            {
                Data = new JsonArray
                {
                    new JsonObject { ["key"] = "global-a" },
                    new JsonObject { ["key"] = "global-b" }
                }
            }
        });

        // Read with no target
        var resNull = (await ctx.GetSchemaDataAsync("sysshared", "data", null, AppSchemaDataResult.List)) as ArrayTypeNode;
        // Read with any non-null target — should see the same global data
        var resAny  = (await ctx.GetSchemaDataAsync("sysshared", "data", "any-tenant", AppSchemaDataResult.List)) as ArrayTypeNode;

        Assert.IsNotNull(resNull);
        Assert.IsNotNull(resAny);
        Assert.AreEqual(2, resNull.Count);
        Assert.AreEqual(resNull.Count, resAny.Count);
    }

    // ─────────────────────────────────────────────────────────────────────
    // ClearAll
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// ClearAll wipes all field data; subsequent queries return an empty result
    /// </summary>
    [TestMethod]
    public async Task AppData_ClearAll()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name   = "test.clearrec",
            Type   = SchemaType.Struct,
            Struct = new StructSchema
            {
                Fields = [new StructFieldSchema { Name = "id", Type = NS_SYSTEM_STRING }]
            }
        });

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name  = "test.clearrecs",
            Type  = SchemaType.Array,
            Array = new ArraySchema { Element = "test.clearrec", Primary = ["id"] }
        });

        await ctx.SaveAppSchemaAsync(new AppSchema { Name = "clearapp" });
        await ctx.SaveAppFieldSchemaAsync("clearapp", new AppFieldSchema
        {
            Name       = "records",
            Type       = "test.clearrecs",
            AllowClear = true
        });

        const string target = "target-c";

        await ctx.PushAppDataAsync("clearapp", target, new Dictionary<string, AppDataFieldPushQuery>
        {
            ["records"] = new AppDataFieldPushQuery
            {
                Data = new JsonArray
                {
                    new JsonObject { ["id"] = "r1" },
                    new JsonObject { ["id"] = "r2" }
                }
            }
        });

        var before = (await ctx.GetSchemaDataAsync("clearapp", "records", target, AppSchemaDataResult.List)) as ArrayTypeNode;
        Assert.AreEqual(2, before?.Count);

        // Clear all records
        await ctx.PushAppDataAsync("clearapp", target, new Dictionary<string, AppDataFieldPushQuery>
        {
            ["records"] = new AppDataFieldPushQuery { ClearAll = true }
        });

        var after = (await ctx.GetSchemaDataAsync("clearapp", "records", target, AppSchemaDataResult.Count));
        Assert.AreEqual(0L, after?.ToValue<long>() ?? 0L);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Exist query
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// AppSchemaDataResult.Exist returns true when data exists and false when the set is empty
    /// </summary>
    [TestMethod]
    public async Task AppData_Query_Exist()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name   = "test.existrec",
            Type   = SchemaType.Struct,
            Struct = new StructSchema
            {
                Fields = [new StructFieldSchema { Name = "id", Type = NS_SYSTEM_STRING }]
            }
        });

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name  = "test.existrecs",
            Type  = SchemaType.Array,
            Array = new ArraySchema { Element = "test.existrec", Primary = ["id"] }
        });

        await ctx.SaveAppSchemaAsync(new AppSchema { Name = "existapp" });
        await ctx.SaveAppFieldSchemaAsync("existapp", new AppFieldSchema { Name = "items", Type = "test.existrecs" });

        const string target = "ex-t1";

        // No data yet → Exist = false
        var empty = await ctx.GetSchemaDataAsync("existapp", "items", target, AppSchemaDataResult.Exist);
        Assert.IsNotNull(empty);
        Assert.IsFalse(empty.ToValue<bool>());

        // Push one record
        await ctx.PushAppDataAsync("existapp", target, new Dictionary<string, AppDataFieldPushQuery>
        {
            ["items"] = new AppDataFieldPushQuery
            {
                Data = new JsonArray { new JsonObject { ["id"] = "e1" } }
            }
        });

        var exist = await ctx.GetSchemaDataAsync("existapp", "items", target, AppSchemaDataResult.Exist);
        Assert.IsNotNull(exist);
        Assert.IsTrue(exist.ToValue<bool>());
    }

    // ─────────────────────────────────────────────────────────────────────
    // Scalar field storage
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A scalar (non-array) field can be written to and read back correctly
    /// </summary>
    [TestMethod]
    public async Task AppData_ScalarField()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveAppSchemaAsync(new AppSchema
        {
            Name        = "scalarfieldapp",
            ScopePolicy = new AppScopePolicy { Type = AppScopeType.SystemLevel }
        });

        await ctx.SaveAppFieldSchemaAsync("scalarfieldapp", new AppFieldSchema
        {
            Name = "version",
            Type = NS_SYSTEM_STRING
        });

        await ctx.PushAppDataAsync("scalarfieldapp", null, new Dictionary<string, AppDataFieldPushQuery>
        {
            ["version"] = new AppDataFieldPushQuery { Data = JsonValue.Create("v2.0") }
        });

        var res = await ctx.GetSchemaDataAsync("scalarfieldapp", "version", null, AppSchemaDataResult.List);
        Assert.IsNotNull(res);
        Assert.AreEqual("v2.0", res.ToValue<string>());
    }

    // ─────────────────────────────────────────────────────────────────────
    // BatchQuery with SystemLevel (no target required)
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// SystemLevel apps do not require a target in batch queries
    /// </summary>
    [TestMethod]
    public async Task BatchQuery_SystemLevel_NoTarget()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name   = "test.syscfg",
            Type   = SchemaType.Struct,
            Struct = new StructSchema
            {
                Fields = [new StructFieldSchema { Name = "name", Type = NS_SYSTEM_STRING }]
            }
        });

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name  = "test.syscfgs",
            Type  = SchemaType.Array,
            Array = new ArraySchema { Element = "test.syscfg", Primary = ["name"] }
        });

        await ctx.SaveAppSchemaAsync(new AppSchema
        {
            Name        = "sysbatchapp",
            ScopePolicy = new AppScopePolicy { Type = AppScopeType.SystemLevel }
        });

        await ctx.SaveAppFieldSchemaAsync("sysbatchapp", new AppFieldSchema { Name = "configs", Type = "test.syscfgs" });

        await ctx.PushAppDataAsync("sysbatchapp", null, new Dictionary<string, AppDataFieldPushQuery>
        {
            ["configs"] = new AppDataFieldPushQuery
            {
                Data = new JsonArray
                {
                    new JsonObject { ["name"] = "debug" },
                    new JsonObject { ["name"] = "verbose" }
                }
            }
        });

        // Target is null — allowed for SystemLevel
        var (results, _) = await ctx.BatchQueryAppDataAsync(
        [
            new AppDataQuery { App = "sysbatchapp", Target = null, Fields = ["configs"] }
        ]);

        Assert.IsNotNull(results);
        Assert.AreEqual(1, results.Length);
        Assert.IsNotNull(results[0].Results?.GetValueOrDefault("configs"));
    }
}
