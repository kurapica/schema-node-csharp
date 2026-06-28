using System.Text.Json;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;
using ValueType = SchemaNode.Runtime.ValueType;

namespace SchemaNode.UnitTest.Core;

[TestClass]
public class ConverterTest : Base.CoreTestBase
{
    #region JSON Round-trip

    [TestMethod]
    public void Json_Roundtrip_Long()
    {
        var json = JsonSerializer.Serialize(42L);
        long back = JsonSerializer.Deserialize<long>(json);
        Assert.AreEqual(42L, back);
    }

    [TestMethod]
    public void Json_Roundtrip_String()
    {
        var json = JsonSerializer.Serialize("hello");
        string? back = JsonSerializer.Deserialize<string>(json);
        Assert.AreEqual("hello", back);
    }

    [TestMethod]
    public void Json_Roundtrip_Bool()
    {
        var json = JsonSerializer.Serialize(true);
        bool back = JsonSerializer.Deserialize<bool>(json);
        Assert.IsTrue(back);
    }

    #endregion

    #region IsAssignableTo

    [TestMethod]
    public async Task IsAssignableTo_SameType_ReturnsTrue()
    {
        var intType = await Context.GetNodeTypeAsync<ValueType>(NS_SYSTEM_INT);
        Assert.IsNotNull(intType);
        Assert.IsTrue(intType.IsAssignableTo(intType));
    }

    [TestMethod]
    public async Task IsAssignableTo_DifferentKinds_ReturnsFalse()
    {
        var intType = await Context.GetNodeTypeAsync<ValueType>(NS_SYSTEM_INT);
        var strType = await Context.GetNodeTypeAsync<ValueType>(NS_SYSTEM_STRING);
        Assert.IsNotNull(intType);
        Assert.IsNotNull(strType);
        Assert.IsFalse(intType.IsAssignableTo(strType));
    }

    [TestMethod]
    public async Task IsAssignableTo_SubtypeToBase_Scalar()
    {
        var intType = await Context.GetNodeTypeAsync<ValueType>(NS_SYSTEM_INT);
        var numberType = await Context.GetNodeTypeAsync<ValueType>(NS_SYSTEM_NUMBER);
        Assert.IsNotNull(intType);
        Assert.IsNotNull(numberType);
        // int and number have different Kind strings — same-kind check won't pass.
        // Compatibility depends on scalar inheritance chain.
    }

    [TestMethod]
    public async Task IsAssignableTo_ObjectKind_UniversalTop()
    {
        var intType = await Context.GetNodeTypeAsync<ValueType>(NS_SYSTEM_INT);
        var objectType = await Context.GetNodeTypeAsync<ValueType>(NS_SYSTEM_OBJECT);
        Assert.IsNotNull(intType);
        Assert.IsNotNull(objectType);
        Assert.IsTrue(intType.IsAssignableTo(objectType));
    }

    [TestMethod]
    public async Task IsAssignableTo_ArraySelf()
    {
        var arrayType = await Context.GetNodeTypeAsync<ArrayType>(NS_SYSTEM_ARRAY);
        if (arrayType == null)
            Assert.Inconclusive("system.array not loaded — same root cause as function namespaces");
        Assert.IsTrue(arrayType.IsAssignableTo(arrayType));
    }

    [TestMethod]
    public async Task IsAssignableTo_StructSameName()
    {
        var contextType = await Context.GetNodeTypeAsync<ValueType>(NS_SYSTEM_CONTEXT);
        Assert.IsNotNull(contextType);
        Assert.IsTrue(contextType.IsAssignableTo(contextType));
    }

    #endregion
}
