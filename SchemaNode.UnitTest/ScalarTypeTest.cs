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
/// Tests for system scalar types and custom scalar types
/// </summary>
[TestClass]
public class ScalarTypeTest : TestBase
{
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

    /// <summary>
    /// Custom scalar with pattern validation: string must match digit pattern
    /// </summary>
    [TestMethod]
    public async Task ScalarType_PatternValidation()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        // Define a custom scalar: 3 uppercase letters followed by 3 digits (e.g., "ABC123")
        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.productcode",
            Type = SchemaType.Scalar,
            Scalar = new ScalarSchema
            {
                Base = NS_SYSTEM_STRING,
                Pattern =
                [
                    new Pattern
                    {
                        Type = PatternType.CharSet,
                        Ranges = [new CharRange { Start = 'A', End = 'Z' }],
                        Min = 3, Max = 3
                    },
                    new Pattern
                    {
                        Type = PatternType.CharSet,
                        Ranges = [new CharRange { Start = '0', End = '9' }],
                        Min = 3, Max = 3
                    },
                ]
            }
        });

        var scalarType = await ctx.GetSchemaTypeAsync<ScalarType>("test.productcode");
        Assert.IsNotNull(scalarType);
        Assert.AreEqual(SchemaNodeStatus.Ready, scalarType.Status);
        Assert.IsNotNull(scalarType.Pattern);
        Assert.AreEqual(2, scalarType.Pattern.Length);

        // Valid: "ABC123"
        var (valid, err1) = await scalarType.ValidateValueAsync(ctx, JsonValue.Create("ABC123")!);
        Assert.IsNotNull(valid, "ABC123 should be valid");
        Assert.IsNull(err1);

        // Invalid: "abc123" (lowercase)
        var (invalid1, err2) = await scalarType.ValidateValueAsync(ctx, JsonValue.Create("abc123")!);
        Assert.IsNull(invalid1, "abc123 should be invalid (lowercase)");
        Assert.IsNotNull(err2);

        // Invalid: "AB1234" (too few letters)
        var (invalid2, err3) = await scalarType.ValidateValueAsync(ctx, JsonValue.Create("AB1234")!);
        Assert.IsNull(invalid2, "AB1234 should be invalid (wrong structure)");
        Assert.IsNotNull(err3);
    }
}
