using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.UnitTest.Core;

/// <summary>
/// Tests for DataNode value access and creation
/// </summary>
[TestClass]
public class NodeSerializationTest : Base.CoreTestBase
{
    [TestMethod]
    public async Task ScalarNode_CreateAndRead()
    {
        var intType = await Context.GetNodeTypeAsync<ScalarType>(NS_SYSTEM_INT);
        Assert.IsNotNull(intType);

        var node = intType.From(42L);
        Assert.AreEqual(42L, node.GetValue<long>());
        Assert.IsFalse(node.IsEmpty);
    }

    [TestMethod]
    public async Task StructNode_CreateAndInspect()
    {
        var contextType = await Context.GetNodeTypeAsync<StructType>(NS_SYSTEM_CONTEXT);
        Assert.IsNotNull(contextType);

        var node = contextType.Create();
        Assert.IsNotNull(node);
        Assert.AreEqual(SCHEMA_KIND_STRUCT, node.Type.Kind);
        Assert.IsTrue(node.IsEmpty);
    }

    [TestMethod]
    public async Task ScalarNode_StringRoundtrip()
    {
        var strType = await Context.GetNodeTypeAsync<ScalarType>(NS_SYSTEM_STRING);
        Assert.IsNotNull(strType);

        var node = strType.From("测试数据");
        Assert.AreEqual("测试数据", node.GetValue<string>());
    }

    [TestMethod]
    public async Task ScalarNode_BoolRoundtrip()
    {
        var boolType = await Context.GetNodeTypeAsync<ScalarType>(NS_SYSTEM_BOOL);
        Assert.IsNotNull(boolType);

        var node = boolType.From(false);
        Assert.AreEqual(false, node.GetValue<bool>());
    }

    [TestMethod]
    public async Task ScalarNode_DateRoundtrip()
    {
        var dateType = await Context.GetNodeTypeAsync<ScalarType>(NS_SYSTEM_DATE);
        Assert.IsNotNull(dateType);

        var dt = DateTimeOffset.UtcNow;
        var node = dateType.From(dt);
        Assert.AreEqual(dt, node.GetValue<DateTimeOffset>());
    }

    [TestMethod]
    public async Task ScalarNode_NumberRoundtrip()
    {
        var numType = await Context.GetNodeTypeAsync<ScalarType>(NS_SYSTEM_NUMBER);
        Assert.IsNotNull(numType);

        var node = numType.From(42.5m);
        Assert.AreEqual(42.5m, node.GetValue<decimal>());
    }

    [TestMethod]
    public async Task DataNode_ClonePreservesValue()
    {
        var intType = await Context.GetNodeTypeAsync<ScalarType>(NS_SYSTEM_INT);
        Assert.IsNotNull(intType);

        var original = intType.From(99L);
        var cloned = original.Clone();
        Assert.IsNotNull(cloned);
        Assert.AreEqual(99L, cloned.GetValue<long>());
        Assert.AreNotSame(original, cloned);
    }

    [TestMethod]
    public async Task DataNode_SetAndClearViolated()
    {
        var intType = await Context.GetNodeTypeAsync<ScalarType>(NS_SYSTEM_INT);
        Assert.IsNotNull(intType);

        var node = intType.From(42L);
        Assert.IsTrue(node.IsValid);

        node.SetViolated("test_violation");
        Assert.IsFalse(node.IsValid);
        Assert.AreEqual("test_violation", node.Violated?.ElementAtOrDefault(0));

        // Clear violations by passing the same violation name
        node.ClearViolated("test_violation");
        Assert.IsTrue(node.IsValid);
    }

    [TestMethod]
    public async Task ScalarNode_NegativeInt()
    {
        var intType = await Context.GetNodeTypeAsync<ScalarType>(NS_SYSTEM_INT);
        Assert.IsNotNull(intType);

        var node = intType.From(-42L);
        Assert.AreEqual(-42L, node.GetValue<long>());
    }

    [TestMethod]
    public async Task ScalarNode_ZeroInt()
    {
        var intType = await Context.GetNodeTypeAsync<ScalarType>(NS_SYSTEM_INT);
        Assert.IsNotNull(intType);

        var node = intType.From(0L);
        // Note: 0 may be treated as empty for numeric scalars
        Console.WriteLine($"Zero Int: IsEmpty={node.IsEmpty}, Value={node.GetValue<long>()}");
        Assert.AreEqual(0L, node.GetValue<long>());
    }
}
