using SchemaNode.Node;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;
using ValueType = SchemaNode.Runtime.ValueType;

namespace SchemaNode.RefactorTest.Core;

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
        var arrayType = await Context.GetNodeTypeAsync<ArrayType>(NS_SYSTEM_ARRAY);
        Assert.IsNotNull(arrayType);

        var objType = await Context.GetNodeTypeAsync<ValueType>(NS_SYSTEM_OBJECT);
        Assert.IsNotNull(objType);
        var resolved = await Context.GetArrayNodeTypeAsync(objType);
        Assert.IsNotNull(resolved, "system.object should resolve to system.array");
    }

    [TestMethod]
    public async Task ArrayNode_Create()
    {
        var arrayType = await Context.GetNodeTypeAsync<ArrayType>(NS_SYSTEM_ARRAY);
        Assert.IsNotNull(arrayType);

        var arrayNode = arrayType.Create();
        Assert.IsNotNull(arrayNode);
        Assert.AreEqual(SCHEMA_KIND_ARRAY, arrayNode.Type.Kind);
    }
}
