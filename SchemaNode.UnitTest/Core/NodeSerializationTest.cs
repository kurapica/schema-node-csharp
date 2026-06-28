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
}
