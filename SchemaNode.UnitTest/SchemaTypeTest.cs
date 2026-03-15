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

[TestClass]
public class SchemaTypeTest : TestBase
{
    // ─────────────────────────────────────────────────────────────────────
    // 1. System scalar types
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verify that system scalar types (system.bool / system.int / system.string) are loaded correctly
    /// </summary>
    [TestMethod]
    public async Task SystemScalarTypes_AreLoaded()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        var boolType = await ctx.GetSchemaTypeAsync<ScalarType>(NS_SYSTEM_BOOL);
        var intType  = await ctx.GetSchemaTypeAsync<ScalarType>(NS_SYSTEM_INT);
        var strType  = await ctx.GetSchemaTypeAsync<ScalarType>(NS_SYSTEM_STRING);

        Assert.IsNotNull(boolType, "system.bool should be loaded");
        Assert.IsNotNull(intType,  "system.int should be loaded");
        Assert.IsNotNull(strType,  "system.string should be loaded");

        Assert.AreEqual(SchemaType.Scalar, boolType.Type);
        Assert.AreEqual(SchemaType.Scalar, intType.Type);
        Assert.AreEqual(SchemaType.Scalar, strType.Type);

        Assert.IsTrue(boolType.IsBool,  "system.bool should be IsBool");
        Assert.IsTrue(intType.IsInt,    "system.int should be IsInt");
        Assert.IsTrue(strType.IsString, "system.string should be IsString");
    }

    /// <summary>
    /// Basic scalar node operations: create a node, set and get its value
    /// </summary>
    [TestMethod]
    public async Task ScalarNode_SetAndGetValue()
    {
        var ctx     = ServiceProvider.GetRequiredService<SchemaContext>();
        var intType = await ctx.GetSchemaTypeAsync<ScalarType>(NS_SYSTEM_INT);
        Assert.IsNotNull(intType);

        var node = intType.CreateNode(42);
        Assert.IsNotNull(node);
        Assert.AreEqual(42L, node.ToValue<long>());

        node.Value = 99;
        Assert.AreEqual(99L, node.ToValue<long>());
    }

    // ─────────────────────────────────────────────────────────────────────
    // 2. Custom EnumType
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
    // 3. Custom StructType
    // ─────────────────────────────────────────────────────────────────────

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

    // ─────────────────────────────────────────────────────────────────────
    // 4. ArrayType
    // ─────────────────────────────────────────────────────────────────────

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

    // ─────────────────────────────────────────────────────────────────────
    // 5. System built-in function calls
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Call system.math.add to verify integer addition
    /// </summary>
    [TestMethod]
    public async Task SystemMath_Add_Int()
    {
        var ctx  = ServiceProvider.GetRequiredService<SchemaContext>();
        var func = await ctx.GetSchemaTypeAsync<FunctionType>("system.math.add");
        Assert.IsNotNull(func);

        var result = await func.CallAsync<long>(ctx, [3L, 5L]);
        Assert.AreEqual(8L, result);
    }

    /// <summary>
    /// Call system.math.multiply to verify multiplication
    /// </summary>
    [TestMethod]
    public async Task SystemMath_Multiply()
    {
        var ctx  = ServiceProvider.GetRequiredService<SchemaContext>();
        var func = await ctx.GetSchemaTypeAsync<FunctionType>("system.math.multiply");
        Assert.IsNotNull(func);

        var result = await func.CallAsync<long>(ctx, [6L, 7L]);
        Assert.AreEqual(42L, result);
    }

    /// <summary>
    /// Call system.math.subtract
    /// </summary>
    [TestMethod]
    public async Task SystemMath_Subtract()
    {
        var ctx  = ServiceProvider.GetRequiredService<SchemaContext>();
        var func = await ctx.GetSchemaTypeAsync<FunctionType>("system.math.subtract");
        Assert.IsNotNull(func);

        var result = await func.CallAsync<long>(ctx, [10L, 3L]);
        Assert.AreEqual(7L, result);
    }

    /// <summary>
    /// Call system.math.percent for percentage calculation
    /// </summary>
    [TestMethod]
    public async Task SystemMath_Percent()
    {
        var ctx  = ServiceProvider.GetRequiredService<SchemaContext>();
        var func = await ctx.GetSchemaTypeAsync<FunctionType>("system.math.numeric.percent");
        Assert.IsNotNull(func);

        // 50 / 200 * 100 = 25.00%
        var result = await func.CallAsync<decimal>(ctx, [50m, 200m, null]);
        Assert.AreEqual(25m, result);
    }

    /// <summary>
    /// Call system.str.concat for string concatenation
    /// </summary>
    [TestMethod]
    public async Task SystemStr_Concat()
    {
        var ctx  = ServiceProvider.GetRequiredService<SchemaContext>();
        var func = await ctx.GetSchemaTypeAsync<FunctionType>("system.str.convert.concat");
        Assert.IsNotNull(func);

        var result = await func.CallAsync<string>(ctx, ["Hello, ", "World!"]);
        Assert.AreEqual("Hello, World!", result);
    }

    /// <summary>
    /// Call system.str.len for string length
    /// </summary>
    [TestMethod]
    public async Task SystemStr_Len()
    {
        var ctx  = ServiceProvider.GetRequiredService<SchemaContext>();
        var func = await ctx.GetSchemaTypeAsync<FunctionType>("system.str.state.len");
        Assert.IsNotNull(func);

        var result = await func.CallAsync<long>(ctx, ["SchemaNode"]);
        Assert.AreEqual(10L, result);
    }

    /// <summary>
    /// Call system.str.trim to strip whitespace
    /// </summary>
    [TestMethod]
    public async Task SystemStr_Trim()
    {
        var ctx  = ServiceProvider.GetRequiredService<SchemaContext>();
        var func = await ctx.GetSchemaTypeAsync<FunctionType>("system.str.convert.trim");
        Assert.IsNotNull(func);

        var result = await func.CallAsync<string>(ctx, ["  hello  "]);
        Assert.AreEqual("hello", result);
    }

    /// <summary>
    /// Call system.logic.and for logical AND
    /// </summary>
    [TestMethod]
    public async Task SystemLogic_AndAlso()
    {
        var ctx  = ServiceProvider.GetRequiredService<SchemaContext>();
        var func = await ctx.GetSchemaTypeAsync<FunctionType>("system.logic.and");
        Assert.IsNotNull(func);

        Assert.IsTrue(await func.CallAsync<bool>(ctx,  [true,  true]));
        Assert.IsFalse(await func.CallAsync<bool>(ctx, [true,  false]));
        Assert.IsFalse(await func.CallAsync<bool>(ctx, [false, true]));
    }

    /// <summary>
    /// Call system.logic.or for logical OR
    /// </summary>
    [TestMethod]
    public async Task SystemLogic_OrElse()
    {
        var ctx  = ServiceProvider.GetRequiredService<SchemaContext>();
        var func = await ctx.GetSchemaTypeAsync<FunctionType>("system.logic.or");
        Assert.IsNotNull(func);

        Assert.IsTrue(await func.CallAsync<bool>(ctx,  [true,  false]));
        Assert.IsTrue(await func.CallAsync<bool>(ctx,  [false, true]));
        Assert.IsFalse(await func.CallAsync<bool>(ctx, [false, false]));
    }

    /// <summary>
    /// Call system.logic.not for logical NOT
    /// </summary>
    [TestMethod]
    public async Task SystemLogic_Not()
    {
        var ctx  = ServiceProvider.GetRequiredService<SchemaContext>();
        var func = await ctx.GetSchemaTypeAsync<FunctionType>("system.logic.not");
        Assert.IsNotNull(func);

        Assert.IsFalse(await func.CallAsync<bool>(ctx, [true]));
        Assert.IsTrue(await func.CallAsync<bool>(ctx,  [false]));
    }

    /// <summary>
    /// Call system.logic.cond for conditional selection
    /// </summary>
    [TestMethod]
    public async Task SystemLogic_Cond()
    {
        var ctx  = ServiceProvider.GetRequiredService<SchemaContext>();
        var func = await ctx.GetSchemaTypeAsync<FunctionType>("system.logic.cond");
        Assert.IsNotNull(func);

        var resultTrue  = await func.CallAsync<long>(ctx, [true,  100L, 200L]);
        var resultFalse = await func.CallAsync<long>(ctx, [false, 100L, 200L]);

        Assert.AreEqual(100L, resultTrue);
        Assert.AreEqual(200L, resultFalse);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 6. Custom FunctionType definition and invocation
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Save custom function double(x) = x + x, call it and verify the result
    /// </summary>
    [TestMethod]
    public async Task CustomFunction_SaveAndCall()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.double",
            Type = SchemaType.Func,
            Func = new FunctionSchema
            {
                Return = NS_SYSTEM_INT,
                Args   = [new FuncArg { Name = "x", Type = NS_SYSTEM_INT }],
                Exps   =
                [
                    new FuncExp
                    {
                        Name   = "result",
                        Func   = "system.math.add",
                        Return = NS_SYSTEM_INT,
                        Args   =
                        [
                            new FuncCallArg { Name = "x" },
                            new FuncCallArg { Name = "x" }
                        ]
                    }
                ]
            }
        });

        var func = await ctx.GetSchemaTypeAsync<FunctionType>("test.double");
        Assert.IsNotNull(func);
        Assert.AreEqual(SchemaType.Func, func.Type);
        Assert.AreEqual(SchemaNodeStatus.Ready, func.Status);

        var result = await func.CallAsync<long>(ctx, [7L]);
        Assert.AreEqual(14L, result);
    }

    /// <summary>
    /// Custom function: negate(x) = 0 - x via a single-expression subtraction
    /// </summary>
    [TestMethod]
    public async Task CustomFunction_Negate()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.negate",
            Type = SchemaType.Func,
            Func = new FunctionSchema
            {
                Return = NS_SYSTEM_INT,
                Args   = [new FuncArg { Name = "x", Type = NS_SYSTEM_INT }],
                Exps   =
                [
                    new FuncExp
                    {
                        Name   = "result",
                        Func   = "system.math.subtract",
                        Return = NS_SYSTEM_INT,
                        Args   =
                        [
                            new FuncCallArg { Value = JsonValue.Create(0L) },
                            new FuncCallArg { Name  = "x" }
                        ]
                    }
                ]
            }
        });

        var func = await ctx.GetSchemaTypeAsync<FunctionType>("test.negate");
        Assert.IsNotNull(func);
        Assert.AreEqual(SchemaNodeStatus.Ready, func.Status);

        var result = await func.CallAsync<long>(ctx, [5L]);
        Assert.AreEqual(-5L, result);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 7. App and field management (AppType / AppFieldType)
    // ─────────────────────────────────────────────────────────────────────

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

    // ─────────────────────────────────────────────────────────────────────
    // 8. App data CRUD (persistence using InMemoryAppDataProvider)
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
    // 9. BatchQueryAppData
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
    // 10. SchemaContext context items
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
    // 11. Schema deletion
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

    // ─────────────────────────────────────────────────────────────────────
    // 12. Type compatibility (CanBeUseAs)
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The same type can be used as itself; incompatible types cannot
    /// </summary>
    [TestMethod]
    public async Task SchemaType_CanBeUseAs()
    {
        var ctx  = ServiceProvider.GetRequiredService<SchemaContext>();
        var intT = (await ctx.GetSchemaTypeAsync<ScalarType>(NS_SYSTEM_INT))!;
        var strT = (await ctx.GetSchemaTypeAsync<ScalarType>(NS_SYSTEM_STRING))!;
        var datT = (await ctx.GetSchemaTypeAsync<ScalarType>(NS_SYSTEM_DATE))!;

        Assert.IsTrue(intT.CanBeUseAs(intT),  "int can be used as int");
        Assert.IsFalse(intT.CanBeUseAs(datT), "int cannot be used as date");
    }

    // ─────────────────────────────────────────────────────────────────────
    // 13. Node JSON serialization
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// ScalarTypeNode.ToJson() produces the correct value
    /// </summary>
    [TestMethod]
    public async Task SchemaNode_ToJson()
    {
        var ctx  = ServiceProvider.GetRequiredService<SchemaContext>();
        var intT = (await ctx.GetSchemaTypeAsync<ScalarType>(NS_SYSTEM_INT))!;
        var node = intT.CreateNode(123);
        Assert.IsNotNull(node);

        var json = node.ToJson();
        Assert.IsNotNull(json);
        Assert.AreEqual(123L, (long)json!);
    }

    /// <summary>
    /// StructTypeNode.ToJson() includes all fields
    /// </summary>
    [TestMethod]
    public async Task StructNode_ToJson()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name   = "test.jsontest",
            Type   = SchemaType.Struct,
            Struct = new StructSchema
            {
                Fields =
                [
                    new StructFieldSchema { Name = "a", Type = NS_SYSTEM_STRING },
                    new StructFieldSchema { Name = "b", Type = NS_SYSTEM_INT }
                ]
            }
        });

        var structType = (await ctx.GetSchemaTypeAsync<StructType>("test.jsontest"))!;
        var node       = structType.CreateNode() as StructTypeNode;
        Assert.IsNotNull(node);

        node["a"] = "foo";
        node["b"] = 99;

        var json = node.ToJson() as JsonObject;
        Assert.IsNotNull(json);
        Assert.AreEqual("foo", (string?)json["a"]);
        Assert.AreEqual(99L,   (long?)  json["b"]);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 14. Additional built-in function tests
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// system.math.divide: division and divide-by-zero protection (returns 0)
    /// </summary>
    [TestMethod]
    public async Task SystemMath_Divide()
    {
        var ctx  = ServiceProvider.GetRequiredService<SchemaContext>();
        var func = await ctx.GetSchemaTypeAsync<FunctionType>("system.math.divide");
        Assert.IsNotNull(func);

        Assert.AreEqual(5L, await func.CallAsync<long>(ctx, [10L, 2L]));
        Assert.AreEqual(0L, await func.CallAsync<long>(ctx, [5L,  0L])); // divide by zero → 0
    }

    /// <summary>
    /// system.math.modulo: modulo operation
    /// </summary>
    [TestMethod]
    public async Task SystemMath_Modulo()
    {
        var ctx  = ServiceProvider.GetRequiredService<SchemaContext>();
        var func = await ctx.GetSchemaTypeAsync<FunctionType>("system.math.modulo");
        Assert.IsNotNull(func);

        Assert.AreEqual(1L, await func.CallAsync<long>(ctx, [10L, 3L]));
        Assert.AreEqual(0L, await func.CallAsync<long>(ctx, [9L,  3L]));
    }

    /// <summary>
    /// system.math.max / min: maximum and minimum values
    /// </summary>
    [TestMethod]
    public async Task SystemMath_Max_Min()
    {
        var ctx  = ServiceProvider.GetRequiredService<SchemaContext>();
        var maxF = await ctx.GetSchemaTypeAsync<FunctionType>("system.math.numeric.max");
        var minF = await ctx.GetSchemaTypeAsync<FunctionType>("system.math.numeric.min");
        Assert.IsNotNull(maxF);
        Assert.IsNotNull(minF);

        Assert.AreEqual(9L, await maxF.CallAsync<long>(ctx, [3L, 9L]));
        Assert.AreEqual(3L, await minF.CallAsync<long>(ctx, [3L, 9L]));
    }

    /// <summary>
    /// system.math.abs: absolute value
    /// </summary>
    [TestMethod]
    public async Task SystemMath_Abs()
    {
        var ctx  = ServiceProvider.GetRequiredService<SchemaContext>();
        var func = await ctx.GetSchemaTypeAsync<FunctionType>("system.math.numeric.abs");
        Assert.IsNotNull(func);

        Assert.AreEqual(7L, await func.CallAsync<long>(ctx, [-7L]));
        Assert.AreEqual(5L, await func.CallAsync<long>(ctx, [5L]));
    }

    /// <summary>
    /// system.str.replace: string replacement
    /// </summary>
    [TestMethod]
    public async Task SystemStr_Replace()
    {
        var ctx  = ServiceProvider.GetRequiredService<SchemaContext>();
        var func = await ctx.GetSchemaTypeAsync<FunctionType>("system.str.convert.replace");
        Assert.IsNotNull(func);

        var result = await func.CallAsync<string>(ctx, ["hello world", "world", "SchemaNode"]);
        Assert.AreEqual("hello SchemaNode", result);
    }

    /// <summary>
    /// system.str.substr: substring extraction
    /// </summary>
    [TestMethod]
    public async Task SystemStr_Substr()
    {
        var ctx  = ServiceProvider.GetRequiredService<SchemaContext>();
        var func = await ctx.GetSchemaTypeAsync<FunctionType>("system.str.convert.substr");
        Assert.IsNotNull(func);

        // "HelloWorld".Substring(5, 10-5) = "World"
        var result = await func.CallAsync<string>(ctx, ["HelloWorld", 5, 10]);
        Assert.AreEqual("World", result);
    }

    /// <summary>
    /// system.str.startswith / endswith: string prefix and suffix check
    /// </summary>
    [TestMethod]
    public async Task SystemStr_StartsWith_EndsWith()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();
        var swF = await ctx.GetSchemaTypeAsync<FunctionType>("system.str.logic.startswith");
        var ewF = await ctx.GetSchemaTypeAsync<FunctionType>("system.str.logic.endswith");
        Assert.IsNotNull(swF);
        Assert.IsNotNull(ewF);

        Assert.IsTrue(await swF.CallAsync<bool>(ctx,  ["SchemaNode", "Schema"]));
        Assert.IsFalse(await swF.CallAsync<bool>(ctx, ["SchemaNode", "Node"]));
        Assert.IsTrue(await ewF.CallAsync<bool>(ctx,  ["SchemaNode", "Node"]));
        Assert.IsFalse(await ewF.CallAsync<bool>(ctx, ["SchemaNode", "Schema"]));
    }

    /// <summary>
    /// system.logic.eq / neq: equality and inequality comparison
    /// </summary>
    [TestMethod]
    public async Task SystemLogic_Equal_NotEqual()
    {
        var ctx  = ServiceProvider.GetRequiredService<SchemaContext>();
        var eqF  = await ctx.GetSchemaTypeAsync<FunctionType>("system.logic.eq");
        var neqF = await ctx.GetSchemaTypeAsync<FunctionType>("system.logic.neq");
        Assert.IsNotNull(eqF);
        Assert.IsNotNull(neqF);

        Assert.IsTrue(await eqF.CallAsync<bool>(ctx,   [42L, 42L]));
        Assert.IsFalse(await eqF.CallAsync<bool>(ctx,  [42L, 0L]));
        Assert.IsTrue(await neqF.CallAsync<bool>(ctx,  [1L, 2L]));
        Assert.IsFalse(await neqF.CallAsync<bool>(ctx, [1L, 1L]));
    }

    /// <summary>
    /// system.logic.isnull / notnull: null check
    /// </summary>
    [TestMethod]
    public async Task SystemLogic_IsNull_NotNull()
    {
        var ctx      = ServiceProvider.GetRequiredService<SchemaContext>();
        var isnullF  = await ctx.GetSchemaTypeAsync<FunctionType>("system.logic.isnull");
        var notnullF = await ctx.GetSchemaTypeAsync<FunctionType>("system.logic.notnull");
        Assert.IsNotNull(isnullF);
        Assert.IsNotNull(notnullF);

        Assert.IsFalse(await isnullF.CallAsync<bool>(ctx,  [42L]));
        Assert.IsTrue(await notnullF.CallAsync<bool>(ctx,  [42L]));
        Assert.IsTrue(await notnullF.CallAsync<bool>(ctx,  [""])); // empty string is not null
    }

    /// <summary>
    /// system.logic.between: range check with inclusive and exclusive boundary variants
    /// </summary>
    [TestMethod]
    public async Task SystemLogic_Between()
    {
        var ctx  = ServiceProvider.GetRequiredService<SchemaContext>();
        var func = await ctx.GetSchemaTypeAsync<FunctionType>("system.logic.between");
        Assert.IsNotNull(func);

        // 5 in (3, 10) exclusive → true
        Assert.IsTrue(await func.CallAsync<bool>(ctx,  [5L, 3L, 10L, null, null]));
        // 3 in (3, 10) exclusive lower → false
        Assert.IsFalse(await func.CallAsync<bool>(ctx, [3L, 3L, 10L, null, null]));
        // 3 in [3, 10) inclusive lower → true
        Assert.IsTrue(await func.CallAsync<bool>(ctx,  [3L, 3L, 10L, true, null]));
        // 10 in (3, 10] inclusive upper → true
        Assert.IsTrue(await func.CallAsync<bool>(ctx,  [10L, 3L, 10L, null, true]));
    }

    /// <summary>
    /// system.logic.isempty / notempty: empty value and empty string detection
    /// </summary>
    [TestMethod]
    public async Task SystemLogic_IsEmpty_NotEmpty()
    {
        var ctx       = ServiceProvider.GetRequiredService<SchemaContext>();
        var isemptyF  = await ctx.GetSchemaTypeAsync<FunctionType>("system.logic.isempty");
        var notemptyF = await ctx.GetSchemaTypeAsync<FunctionType>("system.logic.notempty");
        Assert.IsNotNull(isemptyF);
        Assert.IsNotNull(notemptyF);

        Assert.IsTrue(await isemptyF.CallAsync<bool>(ctx,   [""]));
        Assert.IsFalse(await isemptyF.CallAsync<bool>(ctx,  ["hello"]));
        Assert.IsTrue(await notemptyF.CallAsync<bool>(ctx,  ["hi"]));
        Assert.IsFalse(await notemptyF.CallAsync<bool>(ctx, [""]));
    }

    // ─────────────────────────────────────────────────────────────────────
    // 15. PushAppData error handling
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
    // 16. Target data isolation
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
    // 17. ClearAll
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
    // 18. Exist query
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
    // 19. Scalar field storage
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
    // 20. BatchQuery with SystemLevel (no target required)
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

    // ─────────────────────────────────────────────────────────────────────
    // 21. Multi-step custom function
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Multi-step expression function: square_plus_one(x) = x*x + 1, verifying inter-step result passing
    /// </summary>
    [TestMethod]
    public async Task CustomFunction_MultiStep()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.squareplusone",
            Type = SchemaType.Func,
            Func = new FunctionSchema
            {
                Return = NS_SYSTEM_INT,
                Args   = [new FuncArg { Name = "x", Type = NS_SYSTEM_INT }],
                Exps   =
                [
                    new FuncExp
                    {
                        Name   = "sq",
                        Func   = "system.math.multiply",
                        Return = NS_SYSTEM_INT,
                        Args   =
                        [
                            new FuncCallArg { Name = "x" },
                            new FuncCallArg { Name = "x" }
                        ]
                    },
                    new FuncExp
                    {
                        Name   = "result",
                        Func   = "system.math.add",
                        Return = NS_SYSTEM_INT,
                        Args   =
                        [
                            new FuncCallArg { Name  = "sq" },
                            new FuncCallArg { Value = JsonValue.Create(1L) }
                        ]
                    }
                ]
            }
        });

        var func = await ctx.GetSchemaTypeAsync<FunctionType>("test.squareplusone");
        Assert.IsNotNull(func);
        Assert.AreEqual(SchemaNodeStatus.Ready, func.Status);

        // 4*4 + 1 = 17
        Assert.AreEqual(17L, await func.CallAsync<long>(ctx, [4L]));
        // 0*0 + 1 = 1
        Assert.AreEqual(1L,  await func.CallAsync<long>(ctx, [0L]));
    }

    // ─────────────────────────────────────────────────────────────────────
    // 22. ArrayTypeNode JSON serialization
    // ─────────────────────────────────────────────────────────────────────

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

    // ─────────────────────────────────────────────────────────────────────
    // 23. Extended type compatibility
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A subtype can be used as its base type; the reverse does not hold
    /// </summary>
    [TestMethod]
    public async Task ScalarType_CanBeUseAs_BaseType()
    {
        var ctx    = ServiceProvider.GetRequiredService<SchemaContext>();
        var intT   = (await ctx.GetSchemaTypeAsync<ScalarType>(NS_SYSTEM_INT))!;
        var numT   = (await ctx.GetSchemaTypeAsync<ScalarType>(NS_SYSTEM_NUMBER))!;
        var doubleT = (await ctx.GetSchemaTypeAsync<ScalarType>(NS_SYSTEM_DOUBLE))!;

        // int extends number — int can be used as number
        Assert.IsTrue(intT.CanBeUseAs(numT),    "int can be used as number");
        // double extends number
        Assert.IsTrue(doubleT.CanBeUseAs(numT), "double can be used as number");
        // number is more general — cannot be used as int
        Assert.IsFalse(numT.CanBeUseAs(intT),   "number cannot be used as int");
    }

    /// <summary>
    /// ArrayType CanBeUseAs: arrays with the same element type are compatible; different element types are not
    /// </summary>
    [TestMethod]
    public async Task ArrayType_CanBeUseAs()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name  = "test.intarr",
            Type  = SchemaType.Array,
            Array = new ArraySchema { Element = NS_SYSTEM_INT }
        });

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name  = "test.strarr",
            Type  = SchemaType.Array,
            Array = new ArraySchema { Element = NS_SYSTEM_STRING }
        });

        var intArrT = (await ctx.GetSchemaTypeAsync<ArrayType>("test.intarr"))!;
        var strArrT = (await ctx.GetSchemaTypeAsync<ArrayType>("test.strarr"))!;

        Assert.IsTrue(intArrT.CanBeUseAs(intArrT),   "int-array is compatible with itself");
        // string can be used as int? No — check this direction
        Assert.IsFalse(strArrT.CanBeUseAs(intArrT), "string-array is not compatible with int-array");
    }

    // ─────────────────────────────────────────────────────────────────────
    // 24. Cascading enum values
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
}

