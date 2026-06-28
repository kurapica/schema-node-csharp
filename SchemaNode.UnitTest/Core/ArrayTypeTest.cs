using SchemaNode.Node;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;
using ValueType = SchemaNode.Runtime.ValueType;

namespace SchemaNode.UnitTest.Core;

[TestClass]
public class ArrayTypeTest : Base.CoreTestBase
{
    [TestMethod]
    public async Task ArrayType_Load_SystemArray()
    {
        var arrayType = await Context.GetNodeTypeAsync<ArrayType>(NS_SYSTEM_ARRAY);
        Assert.IsNotNull(arrayType);
        Assert.AreEqual(SCHEMA_KIND_ARRAY, arrayType.Kind);
    }

    [TestMethod]
    public async Task ArrayType_ResolveFromElement()
    {
        var objType = await Context.GetNodeTypeAsync<ValueType>(NS_SYSTEM_OBJECT);
        Assert.IsNotNull(objType);
        var arrayType = await Context.GetArrayNodeTypeAsync(objType);
        Assert.IsNotNull(arrayType);
        Assert.AreEqual(SCHEMA_KIND_ARRAY, arrayType.Kind);
    }

    [TestMethod]
    public async Task ArrayNode_Create()
    {
        var arrayType = await Context.GetNodeTypeAsync<ArrayType>(NS_SYSTEM_ARRAY);
        Assert.IsNotNull(arrayType);
        var node = arrayType.Create() as ArrayNode;
        Assert.IsNotNull(node);
        Assert.AreEqual(SCHEMA_KIND_ARRAY, node.Type.Kind);
    }

    [TestMethod]
    public async Task ArrayNode_AddAndCount()
    {
        var objType = await Context.GetNodeTypeAsync<ValueType>(NS_SYSTEM_OBJECT);
        Assert.IsNotNull(objType);
        var arrayType = await Context.GetArrayNodeTypeAsync(objType);
        Assert.IsNotNull(arrayType);
        var node = arrayType.Create() as ArrayNode;
        Assert.IsNotNull(node);
        node.Add(42L);
        node.Add("hello");
        Assert.AreEqual(2, node.Count);
    }

    [TestMethod]
    public async Task ArrayNode_IndexerAccess()
    {
        var objType = await Context.GetNodeTypeAsync<ValueType>(NS_SYSTEM_OBJECT);
        Assert.IsNotNull(objType);
        var arrayType = await Context.GetArrayNodeTypeAsync(objType);
        Assert.IsNotNull(arrayType);
        var node = arrayType.Create() as ArrayNode;
        Assert.IsNotNull(node);
        node.Add(10);
        node.Add(20);
        // Indexer returns boxed DataNode; extract value via TryGetValue
        var n0 = node[0] as DataNode; Assert.IsNotNull(n0); Assert.IsTrue(n0.TryGetValue(out int v0));
        Assert.AreEqual(10, v0);
        var n1 = node[1] as DataNode; Assert.IsNotNull(n1); Assert.IsTrue(n1.TryGetValue(out int v1));
        Assert.AreEqual(20, v1);
    }
}
