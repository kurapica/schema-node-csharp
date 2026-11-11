using SchemaNode.Attribute;
using SchemaNode.Node;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using static SchemaNode.Function.SystemStr;
using static SchemaNode.Utility.Constant;
using ValueType = SchemaNode.Runtime.ValueType;

namespace SchemaNode.UnitTest.Core;

/// <summary>Element type for the required struct array case.</summary>
[Meta<SchemaType>("test.reqarrayentry")]
public class RequiredArrayEntry
{
    public int Min { get; set; }
}

/// <summary>Struct with a nested struct and a non-nullable array field, so <c>Require</c> is attached to both.</summary>
[Meta<SchemaType>("test.reqarray")]
public class RequiredArrayCase
{
    public RequiredArrayEntry Inner { get; set; } = new();

    public RequiredArrayEntry[] Lines { get; set; } = [];
}

[TestClass]
public class ArrayTypeTest : Base.CoreTestBase
{
    [TestMethod]
    public async Task ArrayType_Load_SystemArray()
    {
        var arrayType = await Context.GetNodeTypeAsync<ArrayType>(NS_SYSTEM_ARRAY);
        Assert.IsNotNull(arrayType);
        Assert.AreEqual(NODE_KIND_ARRAY, arrayType.Kind);
    }

    [TestMethod]
    public async Task ArrayType_ResolveFromElement()
    {
        var objType = await Context.GetNodeTypeAsync<ValueType>(NS_SYSTEM_OBJECT);
        Assert.IsNotNull(objType);
        var arrayType = await Context.GetArrayNodeTypeAsync(objType);
        Assert.IsNotNull(arrayType);
        Assert.AreEqual(NODE_KIND_ARRAY, arrayType.Kind);
    }

    [TestMethod]
    public async Task ArrayNode_Create()
    {
        var arrayType = await Context.GetNodeTypeAsync<ArrayType>(NS_SYSTEM_ARRAY);
        Assert.IsNotNull(arrayType);
        var node = arrayType.Create(Context) as ArrayNode;
        Assert.IsNotNull(node);
        Assert.AreEqual(NODE_KIND_ARRAY, node.Type.Kind);
    }

    [TestMethod]
    public async Task ArrayNode_AddAndCount()
    {
        var objType = await Context.GetNodeTypeAsync<ValueType>(NS_SYSTEM_OBJECT);
        Assert.IsNotNull(objType);
        var arrayType = await Context.GetArrayNodeTypeAsync(objType);
        Assert.IsNotNull(arrayType);
        var node = arrayType.Create(Context) as ArrayNode;
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
        var node = arrayType.Create(Context) as ArrayNode;
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
        var node = arrayType.Create(Context) as ArrayNode;
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
        var node = arrayType.Create(Context) as ArrayNode;
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
        var node = arrayType.Create(Context) as ArrayNode;
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

        var node = arrayType.Create(Context) as ArrayNode;
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
        var node = arrayType.Create(Context) as ArrayNode;
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

    /// <summary>
    /// A required array field that receives its elements through <see cref="ArrayNode.Add"/>
    /// (instead of a whole-value write) must stop being reported as empty.
    /// </summary>
    [TestMethod]
    public async Task ArrayNode_Add_RevalidatesRequiredField()
    {
        Context.FullMode = true;

        var type = await Context.GetNodeTypeAsync<ValueType>("test.reqarray");
        Assert.IsNotNull(type);
        var node = type.Create(Context) as StructNode;
        Assert.IsNotNull(node);

        var lines = node!.GetAccessValue("lines") as ArrayNode;
        Assert.IsNotNull(lines);
        Assert.IsTrue(lines!.IsEmpty, "the array starts empty");

        // Giving the nested struct a value makes the parent struct non-empty, so its StructValue
        // constraint validates the fields and records the required violation on the empty array.
        // This reproduces the state the editor opens with.
        node.GetAccessValue("inner")!.GetAccessValue("min")!.TrySetValue(5);
        await node.ValidateAsync(default);
        Assert.IsFalse(lines.IsValid, "a required empty array violates the constraint");

        var elementType = await Context.GetNodeTypeAsync<ValueType>("test.reqarrayentry");
        Assert.IsNotNull(elementType);
        IValueAccess? row = elementType.Create(Context, new RequiredArrayEntry { Min = 7 });
        Assert.IsNotNull(row);

        lines.Add(row);

        Assert.IsFalse(lines.IsEmpty, "the array now holds an element");
        await WaitUntilAsync(() => lines.IsValid);
        Assert.IsTrue(lines.IsValid, "the required constraint must observe the added element");
        Assert.IsTrue(node.IsValid, "the parent struct must be valid as well");
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        // Data changes are validated on the thread pool.
        for (int i = 0; i < 200; i++)
        {
            if (condition()) return;
            await Task.Delay(10);
        }
        Assert.Fail("validation result was not applied in time");
    }

    [TestMethod]
    public async Task ArrayNode_ElementType()
    {
        var objType = await Context.GetNodeTypeAsync<ValueType>(NS_SYSTEM_OBJECT);
        Assert.IsNotNull(objType);
        var arrayType = await Context.GetArrayNodeTypeAsync(objType);
        Assert.IsNotNull(arrayType);

        var node = arrayType.Create(Context) as ArrayNode;
        Assert.IsNotNull(node);
        Assert.IsNotNull(node!.ElementType);
    }
}
