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
        var n0 = node[0] as IValueAccess; Assert.IsNotNull(n0); Assert.IsTrue(n0.TryGetValue(out int v0));
        Assert.AreEqual(10, v0);
        var n1 = node[1] as IValueAccess; Assert.IsNotNull(n1); Assert.IsTrue(n1.TryGetValue(out int v1));
        Assert.AreEqual(20, v1);
    }

    [TestMethod]
    public async Task ArrayNode_AddRange()
    {
        var objType = await Context.GetNodeTypeAsync<ValueType>(NS_SYSTEM_OBJECT);
        Assert.IsNotNull(objType);
        var arrayType = await Context.GetArrayNodeTypeAsync(objType);
        Assert.IsNotNull(arrayType);
        var node = arrayType.Create() as ArrayNode;
        Assert.IsNotNull(node);

        node!.AddRange(new object[] { 1, 2, 3, 4, 5 });
        Assert.AreEqual(5, node.Count);
    }

    [TestMethod]
    public async Task ArrayNode_ClearValue()
    {
        var objType = await Context.GetNodeTypeAsync<ValueType>(NS_SYSTEM_OBJECT);
        Assert.IsNotNull(objType);
        var arrayType = await Context.GetArrayNodeTypeAsync(objType);
        Assert.IsNotNull(arrayType);
        var node = arrayType.Create() as ArrayNode;
        Assert.IsNotNull(node);

        node!.Add(42L);
        Assert.IsFalse(node.IsEmpty);
        node.ClearValue();
        Assert.IsTrue(node.IsEmpty);
    }

    [TestMethod]
    public async Task ArrayNode_Clone()
    {
        var objType = await Context.GetNodeTypeAsync<ValueType>(NS_SYSTEM_OBJECT);
        Assert.IsNotNull(objType);
        var arrayType = await Context.GetArrayNodeTypeAsync(objType);
        Assert.IsNotNull(arrayType);
        var node = arrayType.Create() as ArrayNode;
        Assert.IsNotNull(node);

        node!.Add(10L);
        node.Add("hello");

        var cloned = node.Clone() as ArrayNode;
        Assert.IsNotNull(cloned);
        Assert.AreEqual(2, cloned!.Count);
        Assert.AreNotSame(node, cloned);
    }

    [TestMethod]
    public async Task ArrayNode_IsEmpty_Initially()
    {
        var arrayType = await Context.GetNodeTypeAsync<ArrayType>(NS_SYSTEM_ARRAY);
        Assert.IsNotNull(arrayType);

        var node = arrayType.Create() as ArrayNode;
        Assert.IsNotNull(node);
        Assert.IsTrue(node!.IsEmpty);
    }

    [TestMethod]
    public async Task ArrayNode_Enumerate()
    {
        var objType = await Context.GetNodeTypeAsync<ValueType>(NS_SYSTEM_OBJECT);
        Assert.IsNotNull(objType);
        var arrayType = await Context.GetArrayNodeTypeAsync(objType);
        Assert.IsNotNull(arrayType);
        var node = arrayType.Create() as ArrayNode;
        Assert.IsNotNull(node);

        node!.Add("first");
        node.Add("second");
        node.Add("third");

        var items = new List<string>();
        foreach (var item in node)
            items.Add(item.GetValue<string>()!);

        Assert.AreEqual(3, items.Count);
        Assert.AreEqual("first", items[0]);
        Assert.AreEqual("second", items[1]);
        Assert.AreEqual("third", items[2]);
    }

    [TestMethod]
    public async Task ArrayNode_ElementType()
    {
        var objType = await Context.GetNodeTypeAsync<ValueType>(NS_SYSTEM_OBJECT);
        Assert.IsNotNull(objType);
        var arrayType = await Context.GetArrayNodeTypeAsync(objType);
        Assert.IsNotNull(arrayType);

        var node = arrayType.Create() as ArrayNode;
        Assert.IsNotNull(node);
        Assert.IsNotNull(node!.ElementType);
    }
}
