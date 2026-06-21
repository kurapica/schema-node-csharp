using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Api.Schema.Application;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Function;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.UnitTest;

/// <summary>
/// Tests for system built-in functions and custom FunctionType definitions
/// </summary>
[TestClass]
public class FunctionTypeTest : TestBase
{
    // ─────────────────────────────────────────────────────────────────────
    // System built-in function calls
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
    // Custom FunctionType definition and invocation
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
    // Extensions built-in function tests
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
    // Multi-step custom function
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
}
