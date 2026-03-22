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
/// Tests for type compatibility (CanBeUseAs) across scalar, struct, and array types
/// </summary>
[TestClass]
public class TypeCompatibilityTest : TestBase
{
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
}
