using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.UnitTest.Core;

/// <summary>
/// Tests for system built-in functions in SchemaNode.Core.
/// Pattern: GetNodeTypeAsync&lt;FunctionType&gt; → CallAsync&lt;T&gt;
/// </summary>
[TestClass]
public class FunctionTypeTest : Base.CoreTestBase
{
    #region System Math

    [TestMethod]
    public async Task SystemMath_Add()
    {
        var func = await Context.GetNodeTypeAsync<FunctionType>("system.math.add");
        Assert.IsNotNull(func);
        Assert.AreEqual(8L, await func.CallAsync<long>(Context, [3L, 5L]));
        Assert.AreEqual(0L, await func.CallAsync<long>(Context, [0L, 0L]));
    }

    [TestMethod]
    public async Task SystemMath_Multiply()
    {
        var func = await Context.GetNodeTypeAsync<FunctionType>("system.math.multiply");
        Assert.IsNotNull(func);
        Assert.AreEqual(42L, await func.CallAsync<long>(Context, [6L, 7L]));
        Assert.AreEqual(0L, await func.CallAsync<long>(Context, [5L, 0L]));
    }

    [TestMethod]
    public async Task SystemMath_Subtract()
    {
        var func = await Context.GetNodeTypeAsync<FunctionType>("system.math.subtract");
        Assert.IsNotNull(func);
        Assert.AreEqual(7L, await func.CallAsync<long>(Context, [10L, 3L]));
    }

    [TestMethod]
    public async Task SystemMath_Divide()
    {
        var func = await Context.GetNodeTypeAsync<FunctionType>("system.math.divide");
        Assert.IsNotNull(func);
        Assert.AreEqual(5L, await func.CallAsync<long>(Context, [10L, 2L]));
        Assert.AreEqual(0L, await func.CallAsync<long>(Context, [5L, 0L])); // ÷0 → 0
    }

    [TestMethod]
    public async Task SystemMath_Modulo()
    {
        var func = await Context.GetNodeTypeAsync<FunctionType>("system.math.modulo");
        Assert.IsNotNull(func);
        Assert.AreEqual(1L, await func.CallAsync<long>(Context, [10L, 3L]));
        Assert.AreEqual(0L, await func.CallAsync<long>(Context, [9L, 3L]));
    }

    #endregion

    #region System Math Numeric

    [TestMethod]
    public async Task SystemMath_Percent()
    {
        var func = await Context.GetNodeTypeAsync<FunctionType>("system.math.numeric.percent");
        Assert.IsNotNull(func);
        Assert.AreEqual(25m, await func.CallAsync<decimal>(Context, [50m, 200m]));
    }

    [TestMethod]
    public async Task SystemMath_Max()
    {
        var func = await Context.GetNodeTypeAsync<FunctionType>("system.math.numeric.max");
        Assert.IsNotNull(func);
        Assert.AreEqual(9L, await func.CallAsync<long>(Context, [3L, 9L]));
    }

    [TestMethod]
    public async Task SystemMath_Min()
    {
        var func = await Context.GetNodeTypeAsync<FunctionType>("system.math.numeric.min");
        Assert.IsNotNull(func);
        Assert.AreEqual(3L, await func.CallAsync<long>(Context, [3L, 9L]));
    }

    [TestMethod]
    public async Task SystemMath_Abs()
    {
        var func = await Context.GetNodeTypeAsync<FunctionType>("system.math.numeric.abs");
        Assert.IsNotNull(func);
        Assert.AreEqual(7L, await func.CallAsync<long>(Context, [-7L]));
        Assert.AreEqual(5L, await func.CallAsync<long>(Context, [5L]));
    }

    #endregion

    #region System String

    [TestMethod]
    public async Task SystemStr_Concat()
    {
        var func = await Context.GetNodeTypeAsync<FunctionType>("system.str.convert.concat");
        Assert.IsNotNull(func);
        Assert.AreEqual("Hello, World!", await func.CallAsync<string>(Context, ["Hello, ", "World!"]));
    }

    [TestMethod]
    public async Task SystemStr_Len()
    {
        var func = await Context.GetNodeTypeAsync<FunctionType>("system.str.state.len");
        Assert.IsNotNull(func);
        Assert.AreEqual(10L, await func.CallAsync<long>(Context, ["SchemaNode"]));
    }

    [TestMethod]
    public async Task SystemStr_Trim()
    {
        var func = await Context.GetNodeTypeAsync<FunctionType>("system.str.convert.trim");
        Assert.IsNotNull(func);
        Assert.AreEqual("hello", await func.CallAsync<string>(Context, ["  hello  "]));
    }

    [TestMethod]
    public async Task SystemStr_Replace()
    {
        var func = await Context.GetNodeTypeAsync<FunctionType>("system.str.convert.replace");
        Assert.IsNotNull(func);
        Assert.AreEqual("hello SchemaNode", await func.CallAsync<string>(Context, ["hello world", "world", "SchemaNode"]));
    }

    [TestMethod]
    public async Task SystemStr_Substr()
    {
        var func = await Context.GetNodeTypeAsync<FunctionType>("system.str.convert.substr");
        Assert.IsNotNull(func);
        Assert.AreEqual("World", await func.CallAsync<string>(Context, ["HelloWorld", 5L, 10L]));
    }

    [TestMethod]
    public async Task SystemStr_StartsWith_EndsWith()
    {
        var swFunc = await Context.GetNodeTypeAsync<FunctionType>("system.str.logic.startswith");
        var ewFunc = await Context.GetNodeTypeAsync<FunctionType>("system.str.logic.endswith");
        Assert.IsNotNull(swFunc);
        Assert.IsNotNull(ewFunc);

        Assert.IsTrue(await swFunc.CallAsync<bool>(Context, ["SchemaNode", "Schema"]));
        Assert.IsFalse(await swFunc.CallAsync<bool>(Context, ["SchemaNode", "Node"]));
        Assert.IsTrue(await ewFunc.CallAsync<bool>(Context, ["SchemaNode", "Node"]));
        Assert.IsFalse(await ewFunc.CallAsync<bool>(Context, ["SchemaNode", "Schema"]));
    }

    #endregion

    #region System Logic

    [TestMethod]
    public async Task SystemLogic_And()
    {
        var func = await Context.GetNodeTypeAsync<FunctionType>("system.logic.and");
        Assert.IsNotNull(func);
        Assert.IsTrue(await func.CallAsync<bool>(Context, [true, true]));
        Assert.IsFalse(await func.CallAsync<bool>(Context, [true, false]));
    }

    [TestMethod]
    public async Task SystemLogic_Or()
    {
        var func = await Context.GetNodeTypeAsync<FunctionType>("system.logic.or");
        Assert.IsNotNull(func);
        Assert.IsTrue(await func.CallAsync<bool>(Context, [false, true]));
        Assert.IsFalse(await func.CallAsync<bool>(Context, [false, false]));
    }

    [TestMethod]
    public async Task SystemLogic_Not()
    {
        var func = await Context.GetNodeTypeAsync<FunctionType>("system.logic.not");
        Assert.IsNotNull(func);
        Assert.IsFalse(await func.CallAsync<bool>(Context, [true]));
        Assert.IsTrue(await func.CallAsync<bool>(Context, [false]));
    }

    [TestMethod]
    public async Task SystemLogic_Cond()
    {
        var func = await Context.GetNodeTypeAsync<FunctionType>("system.logic.cond");
        Assert.IsNotNull(func);
        Assert.AreEqual(100L, await func.CallAsync<long>(Context, [true, 100L, 200L]));
        Assert.AreEqual(200L, await func.CallAsync<long>(Context, [false, 100L, 200L]));
    }

    [TestMethod]
    public async Task SystemLogic_Eq_Neq()
    {
        var eqFunc = await Context.GetNodeTypeAsync<FunctionType>("system.logic.eq");
        var neqFunc = await Context.GetNodeTypeAsync<FunctionType>("system.logic.neq");
        Assert.IsNotNull(eqFunc);
        Assert.IsNotNull(neqFunc);

        Assert.IsTrue(await eqFunc.CallAsync<bool>(Context, [42L, 42L]));
        Assert.IsFalse(await eqFunc.CallAsync<bool>(Context, [42L, 0L]));
        Assert.IsTrue(await neqFunc.CallAsync<bool>(Context, [1L, 2L]));
        Assert.IsFalse(await neqFunc.CallAsync<bool>(Context, [1L, 1L]));
    }

    [TestMethod]
    public async Task SystemLogic_IsNull_NotNull()
    {
        var isNullF = await Context.GetNodeTypeAsync<FunctionType>("system.logic.isnull");
        var notNullF = await Context.GetNodeTypeAsync<FunctionType>("system.logic.notnull");
        Assert.IsNotNull(isNullF);
        Assert.IsNotNull(notNullF);

        Assert.IsFalse(await isNullF.CallAsync<bool>(Context, [42L]));
        Assert.IsTrue(await notNullF.CallAsync<bool>(Context, [42L]));
        Assert.IsTrue(await notNullF.CallAsync<bool>(Context, [""]));
    }

    [TestMethod]
    public async Task SystemLogic_Between()
    {
        var func = await Context.GetNodeTypeAsync<FunctionType>("system.logic.between");
        Assert.IsNotNull(func);
        Assert.IsTrue(await func.CallAsync<bool>(Context, [5L, 1L, 10L]));
        Assert.IsFalse(await func.CallAsync<bool>(Context, [0L, 1L, 10L]));
    }

    [TestMethod]
    public async Task SystemLogic_IsEmpty_NotEmpty()
    {
        var isEmptyF = await Context.GetNodeTypeAsync<FunctionType>("system.logic.isempty");
        var notEmptyF = await Context.GetNodeTypeAsync<FunctionType>("system.logic.notempty");
        Assert.IsNotNull(isEmptyF);
        Assert.IsNotNull(notEmptyF);

        Assert.IsTrue(await isEmptyF.CallAsync<bool>(Context, [""]));
        Assert.IsTrue(await notEmptyF.CallAsync<bool>(Context, ["data"]));
    }

    #endregion
}
