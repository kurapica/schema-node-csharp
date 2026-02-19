﻿using System.Text.Json.Nodes;
 using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SchemaNode.Api.Schema.Application;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.UnitTest
{
    [TestClass]
    public class SchemaTypeTest : TestBase
    {
        [TestMethod]
        public async Task ComplexAppTest()
        {
            var schemaContext = ServiceProvider.GetRequiredService<SchemaContext>();
            
            // 1. Define a simple scalar type
            await schemaContext.SaveSchemaAsync(new NodeSchema
            {
                Name = "test",
                Type = SchemaType.Namespace,
                Schemas = [
                    new NodeSchema
                    {
                        Name = "test.enum",
                        Type = SchemaType.Enum,
                        Enum = new EnumSchema {
                            Type = EnumValueType.Int,
                            Values =
                            [
                                new EnumValueInfo
                                {
                                    Value = "1",
                                    Name = "Value1"
                                },
                                new EnumValueInfo
                                {
                                    Value = "2",
                                    Name = "Value2"
                                }
                            ]
                        }
                    },
                    new NodeSchema
                    {
                        Name = "test.struct",
                        Type = SchemaType.Struct,
                        Struct =  new StructSchema {
                            Fields =
                            [
                                new StructFieldConfig
                                {
                                    Name = "type",
                                    Type = "test.enum"
                                },
                                new StructFieldConfig
                                {
                                    Name = "name",
                                    Type = NS_SYSTEM_STRING
                                },
                                new StructFieldConfig
                                {
                                    Name = "age",
                                    Type = NS_SYSTEM_INT,
                                    LowLimit = "0"
                                }
                            ]
                        }
                    },
                    new NodeSchema
                    {
                        Name = "test.array",
                        Type = SchemaType.Array,
                        Array = new ArraySchema
                        {
                            Element = "test.struct",
                            Primary = ["name"]
                        }
                    }
                ]
            });

            // 2. Define an AppType to store the data
            await schemaContext.SaveAppSchemaAsync(new AppSchema
            {
                Name = "test",
            });
            
            await schemaContext.SaveAppFieldSchemaAsync("test", new AppFieldSchema
            {
                Name = "value",
                Type = "test.array"
            });

            // 3. Write data
            await schemaContext.PushAppDataAsync("test", Guid.Empty.ToString(), new Dictionary<string, AppDataFieldPushQuery>
            {
                ["value"] = new AppDataFieldPushQuery
                {
                    Data = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = 1,
                            ["name"] = "Alice",
                            ["age"] = 30
                        },
                        new JsonObject
                        {
                            ["type"] = 2,
                            ["name"] = "Bob",
                            ["age"] = 25
                        }
                    }
                }
            });

            // 4. Read the data back and verify
            ArrayTypeNode? res = (await schemaContext.GetSchemaDataAsync("test", "value", Guid.Empty.ToString(), AppSchemaDataResult.List)) as ArrayTypeNode;
            Assert.IsNotNull(res);
            Assert.AreEqual(2, res.Count);
        }
    }
}

