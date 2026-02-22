using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SchemaNode.Api.Schema.Application;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.UnitTest
{
    [TestClass]
    public class SchemaTypeTest : TestBase
    {
        // ─────────────────────────────────────────────────────────────────────
        // 1. 系统基础类型 (System scalar types)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 验证系统标量类型 (system.bool / system.int / system.string) 正确加载
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
        /// 标量节点基本值操作：创建节点、设置/读取值
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
        // 2. 自定义枚举类型 (Custom EnumType)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 保存整型枚举 Schema 并验证其加载后的结构
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
            Assert.AreEqual(3, enumType.Root.SubList?.Length ?? 0);
        }

        /// <summary>
        /// 枚举节点可以正确存储和读取值
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
        // 3. 自定义结构类型 (Custom StructType)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 保存 StructType 并验证字段映射正确
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
                        new StructFieldConfig { Name = "name", Type = NS_SYSTEM_STRING },
                        new StructFieldConfig { Name = "age",  Type = NS_SYSTEM_INT }
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
        /// StructTypeNode 字段读写操作
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
                        new StructFieldConfig { Name = "x", Type = NS_SYSTEM_INT },
                        new StructFieldConfig { Name = "y", Type = NS_SYSTEM_INT }
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
        /// StructType 支持继承：子类型的 BaseNode 指向父类型
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
                    Fields = [new StructFieldConfig { Name = "id", Type = NS_SYSTEM_STRING }]
                }
            });

            await ctx.SaveSchemaAsync(new NodeSchema
            {
                Name   = "test.child",
                Type   = SchemaType.Struct,
                Struct = new StructSchema
                {
                    Base   = "test.base",
                    Fields = [new StructFieldConfig { Name = "extra", Type = NS_SYSTEM_INT }]
                }
            });

            var childType = await ctx.GetSchemaTypeAsync<StructType>("test.child");
            Assert.IsNotNull(childType);
            Assert.IsNotNull(childType.BaseNode, "BaseNode should be set");
            Assert.AreEqual("test.base", childType.Base);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 4. 数组类型 (ArrayType)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 保存 ArrayType，验证元素类型和主键正确
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
                        new StructFieldConfig { Name = "code",  Type = NS_SYSTEM_STRING },
                        new StructFieldConfig { Name = "value", Type = NS_SYSTEM_INT }
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
        /// ArrayTypeNode 添加和读取元素
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
                    Fields = [new StructFieldConfig { Name = "tag", Type = NS_SYSTEM_STRING }]
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
        // 5. 系统内置函数调用 (System built-in function calls)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 调用 system.math.add 验证整数加法
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
        /// 调用 system.math.multiply 验证乘法
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
        /// 调用 system.math.subtract
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
        /// 调用 system.math.percent 百分比计算
        /// </summary>
        [TestMethod]
        public async Task SystemMath_Percent()
        {
            var ctx  = ServiceProvider.GetRequiredService<SchemaContext>();
            var func = await ctx.GetSchemaTypeAsync<FunctionType>("system.math.percent");
            Assert.IsNotNull(func);

            // 50 / 200 * 100 = 25.00%
            var result = await func.CallAsync<decimal>(ctx, [50m, 200m, null]);
            Assert.AreEqual(25m, result);
        }

        /// <summary>
        /// 调用 system.str.concat 字符串拼接
        /// </summary>
        [TestMethod]
        public async Task SystemStr_Concat()
        {
            var ctx  = ServiceProvider.GetRequiredService<SchemaContext>();
            var func = await ctx.GetSchemaTypeAsync<FunctionType>("system.str.concat");
            Assert.IsNotNull(func);

            var result = await func.CallAsync<string>(ctx, ["Hello, ", "World!"]);
            Assert.AreEqual("Hello, World!", result);
        }

        /// <summary>
        /// 调用 system.str.len 字符串长度
        /// </summary>
        [TestMethod]
        public async Task SystemStr_Len()
        {
            var ctx  = ServiceProvider.GetRequiredService<SchemaContext>();
            var func = await ctx.GetSchemaTypeAsync<FunctionType>("system.str.len");
            Assert.IsNotNull(func);

            var result = await func.CallAsync<long>(ctx, ["SchemaNode"]);
            Assert.AreEqual(10L, result);
        }

        /// <summary>
        /// 调用 system.str.trim 去除空白
        /// </summary>
        [TestMethod]
        public async Task SystemStr_Trim()
        {
            var ctx  = ServiceProvider.GetRequiredService<SchemaContext>();
            var func = await ctx.GetSchemaTypeAsync<FunctionType>("system.str.trim");
            Assert.IsNotNull(func);

            var result = await func.CallAsync<string>(ctx, ["  hello  "]);
            Assert.AreEqual("hello", result);
        }

        /// <summary>
        /// 调用 system.logic.andalso 逻辑与
        /// </summary>
        [TestMethod]
        public async Task SystemLogic_AndAlso()
        {
            var ctx  = ServiceProvider.GetRequiredService<SchemaContext>();
            var func = await ctx.GetSchemaTypeAsync<FunctionType>("system.logic.andalso");
            Assert.IsNotNull(func);

            Assert.IsTrue(await func.CallAsync<bool>(ctx,  [true,  true]));
            Assert.IsFalse(await func.CallAsync<bool>(ctx, [true,  false]));
            Assert.IsFalse(await func.CallAsync<bool>(ctx, [false, true]));
        }

        /// <summary>
        /// 调用 system.logic.orelse 逻辑或
        /// </summary>
        [TestMethod]
        public async Task SystemLogic_OrElse()
        {
            var ctx  = ServiceProvider.GetRequiredService<SchemaContext>();
            var func = await ctx.GetSchemaTypeAsync<FunctionType>("system.logic.orelse");
            Assert.IsNotNull(func);

            Assert.IsTrue(await func.CallAsync<bool>(ctx,  [true,  false]));
            Assert.IsTrue(await func.CallAsync<bool>(ctx,  [false, true]));
            Assert.IsFalse(await func.CallAsync<bool>(ctx, [false, false]));
        }

        /// <summary>
        /// 调用 system.logic.not 逻辑非
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
        /// 调用 system.logic.cond 条件选择
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
        // 6. 自定义函数定义与调用 (Custom FunctionType)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 保存自定义函数 double(x) = x + x，调用并验证结果
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
        /// 自定义函数：通过多步表达式 negate(x) = 0 - x
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
        // 7. 应用与字段管理 (AppType / AppFieldType)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 保存 AppSchema，验证 AppType 正确加载
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
        /// 向应用添加字段，验证字段列表
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
        /// 嵌套 AppSchema（子应用）创建与加载
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
        // 8. 应用数据 CRUD (App data persistence using InMemoryAppDataProvider)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 完整流程：定义类型 → 应用 → 写入 → 读取验证（复现原始示例）
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
                                new StructFieldConfig { Name = "type", Type = "test.enum" },
                                new StructFieldConfig { Name = "name", Type = NS_SYSTEM_STRING },
                                new StructFieldConfig { Name = "age",  Type = NS_SYSTEM_INT, LowLimit = "0" }
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
        /// SystemLevel 应用：无需 target，数据全局共享
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
                    Fields = [new StructFieldConfig { Name = "key", Type = NS_SYSTEM_STRING }]
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
        /// 更新数据：通过主键匹配更新已存在条目
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
                        new StructFieldConfig { Name = "sku",   Type = NS_SYSTEM_STRING },
                        new StructFieldConfig { Name = "price", Type = NS_SYSTEM_INT }
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

            // 初始写入
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

            // 更新 price
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
        /// 删除数据条目，剩余条目数量正确
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
                        new StructFieldConfig { Name = "id",    Type = NS_SYSTEM_STRING },
                        new StructFieldConfig { Name = "title", Type = NS_SYSTEM_STRING }
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

            // 删除 id=1
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
        /// AppSchemaDataResult.Count 返回正确数量
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
                    Fields = [new StructFieldConfig { Name = "nid", Type = NS_SYSTEM_STRING }]
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
        /// AppSchemaDataResult.First / Last 返回正确的首尾元素
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
                        new StructFieldConfig { Name = "seq",     Type = NS_SYSTEM_STRING },
                        new StructFieldConfig { Name = "message", Type = NS_SYSTEM_STRING }
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
        // 9. 批量查询 (BatchQueryAppData)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// BatchQueryAppData 可以同时查询多个字段并返回结果
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
                        new StructFieldConfig { Name = "k", Type = NS_SYSTEM_STRING },
                        new StructFieldConfig { Name = "v", Type = NS_SYSTEM_STRING }
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
        // 10. SchemaContext 上下文项 (Context Items)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// SetContextItem / GetContextItem 泛型存取
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
        /// 未设置的上下文项返回 null
        /// </summary>
        [TestMethod]
        public void ContextItem_GetMissing_ReturnsNull()
        {
            var ctx = ServiceProvider.GetRequiredService<SchemaContext>();
            var val = ctx.GetContextItem<List<int>>();
            Assert.IsNull(val);
        }

        /// <summary>
        /// TryGetContextItem 行为验证
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
        /// GetOrCreateContextItem 未存在时自动创建，再次获取返回同一实例
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
        // 11. Schema 删除 (DeleteSchema)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 保存后删除，再查找应返回 null
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
                    Fields = [new StructFieldConfig { Name = "x", Type = NS_SYSTEM_INT }]
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
        // 12. 类型兼容性 (CanBeUseAs)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 同类型可相互使用，不同类型不可
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
        // 13. 节点 JSON 序列化
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// ScalarTypeNode.ToJson() 产出正确值
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
        /// StructTypeNode.ToJson() 包含所有字段
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
                        new StructFieldConfig { Name = "a", Type = NS_SYSTEM_STRING },
                        new StructFieldConfig { Name = "b", Type = NS_SYSTEM_INT }
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
    }
}

