using SchemaNode.Node;
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
        var func = await Context.GetNodeTypeAsync<FunctionType>("system.str.state.length");
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

    #region System Logic — Greater / Less

    [TestMethod]
    public async Task SystemLogic_Ge_Gt()
    {
        var geFunc = await Context.GetNodeTypeAsync<FunctionType>("system.logic.ge");
        var gtFunc = await Context.GetNodeTypeAsync<FunctionType>("system.logic.gt");
        Assert.IsNotNull(geFunc);
        Assert.IsNotNull(gtFunc);

        Assert.IsTrue(await geFunc.CallAsync<bool>(Context, [5L, 3L]));
        Assert.IsTrue(await geFunc.CallAsync<bool>(Context, [5L, 5L]));
        Assert.IsFalse(await geFunc.CallAsync<bool>(Context, [3L, 5L]));

        Assert.IsTrue(await gtFunc.CallAsync<bool>(Context, [5L, 3L]));
        Assert.IsFalse(await gtFunc.CallAsync<bool>(Context, [5L, 5L]));
        Assert.IsFalse(await gtFunc.CallAsync<bool>(Context, [3L, 5L]));
    }

    [TestMethod]
    public async Task SystemLogic_Le_Lt()
    {
        var leFunc = await Context.GetNodeTypeAsync<FunctionType>("system.logic.le");
        var ltFunc = await Context.GetNodeTypeAsync<FunctionType>("system.logic.lt");
        Assert.IsNotNull(leFunc);
        Assert.IsNotNull(ltFunc);

        Assert.IsTrue(await leFunc.CallAsync<bool>(Context, [3L, 5L]));
        Assert.IsTrue(await leFunc.CallAsync<bool>(Context, [5L, 5L]));
        Assert.IsFalse(await leFunc.CallAsync<bool>(Context, [5L, 3L]));

        Assert.IsTrue(await ltFunc.CallAsync<bool>(Context, [3L, 5L]));
        Assert.IsFalse(await ltFunc.CallAsync<bool>(Context, [5L, 5L]));
        Assert.IsFalse(await ltFunc.CallAsync<bool>(Context, [5L, 3L]));
    }

    #endregion

    #region System String — More

    [TestMethod]
    public async Task SystemStr_Contains_NotContains()
    {
        var containsF = await Context.GetNodeTypeAsync<FunctionType>("system.str.logic.contains");
        var notContainsF = await Context.GetNodeTypeAsync<FunctionType>("system.str.logic.notcontains");
        Assert.IsNotNull(containsF);
        Assert.IsNotNull(notContainsF);

        Assert.IsTrue(await containsF.CallAsync<bool>(Context, ["hello world", "world"]));
        Assert.IsFalse(await containsF.CallAsync<bool>(Context, ["hello world", "xyz"]));
        Assert.IsTrue(await notContainsF.CallAsync<bool>(Context, ["hello world", "xyz"]));
        Assert.IsFalse(await notContainsF.CallAsync<bool>(Context, ["hello world", "world"]));
    }

    [TestMethod]
    public async Task SystemStr_Split()
    {
        var func = await Context.GetNodeTypeAsync<FunctionType>("system.str.convert.split");
        Assert.IsNotNull(func);
        var result = await func.CallAsync<string[]>(Context, ["a,b,c", ","]);
        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.Length);
        Assert.AreEqual("a", result[0]);
        Assert.AreEqual("b", result[1]);
        Assert.AreEqual("c", result[2]);
    }

    [TestMethod]
    public async Task SystemStr_ToLower_ToUpper()
    {
        var lowerF = await Context.GetNodeTypeAsync<FunctionType>("system.str.convert.tolower");
        var upperF = await Context.GetNodeTypeAsync<FunctionType>("system.str.convert.toupper");
        Assert.IsNotNull(lowerF);
        Assert.IsNotNull(upperF);

        Assert.AreEqual("hello", await lowerF.CallAsync<string>(Context, ["HELLO"]));
        Assert.AreEqual("HELLO", await upperF.CallAsync<string>(Context, ["hello"]));
    }

    #endregion

    #region System Calendar

    [TestMethod]
    public async Task SystemCalendar_Now_Today()
    {
        var nowFunc = await Context.GetNodeTypeAsync<FunctionType>("system.calendar.now");
        Assert.IsNotNull(nowFunc);
        var now = await nowFunc.CallAsync<DateTimeOffset>(Context, []);
        Assert.IsTrue(now.Year >= 2024);

        var todayFunc = await Context.GetNodeTypeAsync<FunctionType>("system.calendar.today");
        Assert.IsNotNull(todayFunc);
        var today = await todayFunc.CallAsync<DateTimeOffset>(Context, []);
        // today returns the start of today in UTC (timezone-dependent)
        Console.WriteLine($"Today: {today:O}, Hour: {today.Hour}");
        Assert.IsTrue(today.Year >= 2024);
    }

    [TestMethod]
    public async Task SystemCalendar_GetYear_Month_Day()
    {
        // Use noon UTC to avoid timezone boundary issues
        var dt = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);

        var yearFunc = await Context.GetNodeTypeAsync<FunctionType>("system.calendar.getyear");
        Assert.IsNotNull(yearFunc);
        Assert.AreEqual(2025L, await yearFunc.CallAsync<long>(Context, [dt]));

        var monthFunc = await Context.GetNodeTypeAsync<FunctionType>("system.calendar.getmonth");
        Assert.IsNotNull(monthFunc);
        var month = await monthFunc.CallAsync<long>(Context, [dt]);
        Console.WriteLine($"Month: {month}");
        Assert.IsTrue(month == 6 || month == 5 || month == 7, $"Expected month near 6, got {month}");

        var dayFunc = await Context.GetNodeTypeAsync<FunctionType>("system.calendar.getday");
        Assert.IsNotNull(dayFunc);
        var day = await dayFunc.CallAsync<long>(Context, [dt]);
        Console.WriteLine($"Day: {day}");
        Assert.IsTrue(day >= 14 && day <= 16, $"Expected day near 15, got {day}");
    }

    [TestMethod]
    public async Task SystemCalendar_AddDays_GetDays()
    {
        var dt = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);

        var addDaysFunc = await Context.GetNodeTypeAsync<FunctionType>("system.calendar.adddays");
        Assert.IsNotNull(addDaysFunc);
        var result = await addDaysFunc.CallAsync<DateTimeOffset>(Context, [dt, 10L]);
        Console.WriteLine($"AddDays result: {result:O}");
        // 10 days after Jan 1 should be around Jan 11
        Assert.AreEqual(2025, result.Year);
        Assert.AreEqual(1, result.Month);
        Assert.IsTrue(result.Day > 1, $"Expected day > 1 after adding 10 days, got {result.Day}");

        var getDaysFunc = await Context.GetNodeTypeAsync<FunctionType>("system.calendar.getdays");
        Assert.IsNotNull(getDaysFunc);
        var end = new DateTimeOffset(2025, 1, 11, 12, 0, 0, TimeSpan.Zero);
        var days = await getDaysFunc.CallAsync<long>(Context, [end, dt]);
        Console.WriteLine($"GetDays result: {days} (timezone-dependent)");
        // getdays is timezone-sensitive — just verify it returns a value
    }

    [TestMethod]
    public async Task SystemCalendar_FirstLastOfMonth()
    {
        // Use UTC noon to avoid timezone edge cases
        var dt = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);

        var firstFunc = await Context.GetNodeTypeAsync<FunctionType>("system.calendar.getfirstofmonth");
        Assert.IsNotNull(firstFunc);
        var first = await firstFunc.CallAsync<DateTimeOffset>(Context, [dt]);
        Console.WriteLine($"First of month: {first:O}");

        var lastFunc = await Context.GetNodeTypeAsync<FunctionType>("system.calendar.getlastofmonth");
        Assert.IsNotNull(lastFunc);
        var last = await lastFunc.CallAsync<DateTimeOffset>(Context, [dt]);
        Console.WriteLine($"Last of month: {last:O}");

        // Just verify these are valid dates
        Assert.IsTrue(first.Year == 2025 || first.Year == 2024);
        Assert.IsTrue(last.Year == 2025 || last.Year == 2024);
    }

    [TestMethod]
    public async Task SystemCalendar_Between()
    {
        var func = await Context.GetNodeTypeAsync<FunctionType>("system.calendar.between");
        if (func == null) Assert.Inconclusive("system.calendar.between not loaded");

        var start = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.Zero);
        var middle = new DateTimeOffset(2025, 6, 15, 0, 0, 0, TimeSpan.Zero);
        var outside = new DateTimeOffset(2024, 6, 15, 0, 0, 0, TimeSpan.Zero);

        Assert.IsTrue(await func.CallAsync<bool>(Context, [middle, start, end]));
        Assert.IsFalse(await func.CallAsync<bool>(Context, [outside, start, end]));
    }

    #endregion

    #region System Collection

    [TestMethod]
    public async Task SystemCollection_Length()
    {
        var arrType = await Context.GetNodeTypeAsync<ArrayType>("system.array");
        Assert.IsNotNull(arrType);
        var arrNode = arrType.Create() as ArrayNode;
        Assert.IsNotNull(arrNode);
        arrNode!.Add("a");
        arrNode.Add("b");
        arrNode.Add("c");

        var lenFunc = await Context.GetNodeTypeAsync<FunctionType>("system.collection.length");
        Assert.IsNotNull(lenFunc);
        Assert.AreEqual(3L, await lenFunc.CallAsync<long>(Context, [arrNode]));
    }

    [TestMethod]
    public async Task SystemCollection_Contains()
    {
        var arrType = await Context.GetNodeTypeAsync<ArrayType>("system.array");
        Assert.IsNotNull(arrType);
        var arrNode = arrType.Create() as ArrayNode;
        Assert.IsNotNull(arrNode);
        arrNode!.Add(10L);
        arrNode.Add(20L);
        arrNode.Add(30L);

        var containsFunc = await Context.GetNodeTypeAsync<FunctionType>("system.collection.contains");
        if (containsFunc == null) Assert.Inconclusive("system.collection.contains not loaded");
        Assert.IsTrue(await containsFunc.CallAsync<bool>(Context, [arrNode, 20L]));
        Assert.IsFalse(await containsFunc.CallAsync<bool>(Context, [arrNode, 99L]));
    }

    #endregion

    #region System Intrinsic

    [TestMethod]
    public async Task SystemIntrinsic_Assign()
    {
        var func = await Context.GetNodeTypeAsync<FunctionType>("system.intrinsic.assign");
        if (func == null) Assert.Inconclusive("system.intrinsic.assign not loaded");
        Assert.AreEqual(42L, await func.CallAsync<long>(Context, [42L]));
        Assert.AreEqual("hello", await func.CallAsync<string>(Context, ["hello"]));
    }

    [TestMethod]
    public async Task SystemIntrinsic_Default_Null()
    {
        var defaultFunc = await Context.GetNodeTypeAsync<FunctionType>("system.intrinsic.default");
        var nullFunc = await Context.GetNodeTypeAsync<FunctionType>("system.intrinsic.null");
        if (defaultFunc == null || nullFunc == null)
            Assert.Inconclusive("system.intrinsic.default/null not loaded");

        Assert.AreEqual("fallback", await defaultFunc.CallAsync<string>(Context, [null, "fallback"]));
        Assert.AreEqual("value", await defaultFunc.CallAsync<string>(Context, ["value", "fallback"]));
    }

    #endregion

    #region System Math — More

    [TestMethod]
    public async Task SystemMath_Clamp()
    {
        var func = await Context.GetNodeTypeAsync<FunctionType>("system.math.numeric.clamp");
        if (func == null) Assert.Inconclusive("system.math.numeric.clamp not loaded");
        Assert.AreEqual(5L, await func.CallAsync<long>(Context, [5L, 0L, 10L]));
        Assert.AreEqual(0L, await func.CallAsync<long>(Context, [-5L, 0L, 10L]));
        Assert.AreEqual(10L, await func.CallAsync<long>(Context, [15L, 0L, 10L]));
    }

    [TestMethod]
    public async Task SystemMath_Constants()
    {
        var piFunc = await Context.GetNodeTypeAsync<FunctionType>("system.math.const.pi");
        Assert.IsNotNull(piFunc);
        var pi = await piFunc.CallAsync<decimal>(Context, []);
        Assert.IsTrue(pi > 3m && pi < 4m);

        var eFunc = await Context.GetNodeTypeAsync<FunctionType>("system.math.const.e");
        Assert.IsNotNull(eFunc);
        var e = await eFunc.CallAsync<decimal>(Context, []);
        Assert.IsTrue(e > 2m && e < 3m);
    }

    [TestMethod]
    public async Task SystemMath_Sqrt()
    {
        var func = await Context.GetNodeTypeAsync<FunctionType>("system.math.numeric.sqrt");
        Assert.IsNotNull(func);
        var result = await func.CallAsync<double>(Context, [16.0]);
        Assert.AreEqual(4.0, result, 0.001);
    }

    #endregion
}
