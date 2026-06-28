using System.Text.Json.Nodes;
using SchemaNode.Node;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.UnitTest.Core;

[TestClass]
public class ScalarTypeTest : Base.CoreTestBase
{
    [TestMethod]
    public async Task SystemScalarTypes_AreLoaded()
    {
        var boolType = await Context.GetNodeTypeAsync<ScalarType>(NS_SYSTEM_BOOL);
        var intType  = await Context.GetNodeTypeAsync<ScalarType>(NS_SYSTEM_INT);
        var strType  = await Context.GetNodeTypeAsync<ScalarType>(NS_SYSTEM_STRING);
        var dateType = await Context.GetNodeTypeAsync<ScalarType>(NS_SYSTEM_DATE);
        var numType  = await Context.GetNodeTypeAsync<ScalarType>(NS_SYSTEM_NUMBER);

        Assert.IsNotNull(boolType);
        Assert.IsNotNull(intType);
        Assert.IsNotNull(strType);
        Assert.IsNotNull(dateType);
        Assert.IsNotNull(numType);

        Assert.AreEqual(SCHEMA_KIND_BOOL,   boolType.Kind);
        Assert.AreEqual(SCHEMA_KIND_INT,    intType.Kind);
        Assert.AreEqual(SCHEMA_KIND_STRING, strType.Kind);
    }

    [TestMethod]
    public async Task ScalarNode_SetAndGetValue()
    {
        var intType = await Context.GetNodeTypeAsync<ScalarType>(NS_SYSTEM_INT);
        Assert.IsNotNull(intType);

        var node = intType.From(42L);
        Assert.IsNotNull(node);
        Assert.AreEqual(42L, node.GetValue<long>());
        Assert.IsFalse(node.IsEmpty);
    }

    [TestMethod]
    public async Task ScalarNode_Validate_ValidValue()
    {
        var intType = await Context.GetNodeTypeAsync<ScalarType>(NS_SYSTEM_INT);
        Assert.IsNotNull(intType);

        DataNode node = await intType.ValidateValueAsync(Context, JsonValue.Create(10L)!);
        Assert.IsNotNull(node);
        Assert.AreEqual(10L, node.GetValue<long>());
        Assert.IsTrue(node.Violated is null or { IsEmpty: true });
    }

    [TestMethod]
    public async Task ScalarNode_StringType()
    {
        var strType = await Context.GetNodeTypeAsync<ScalarType>(NS_SYSTEM_STRING);
        Assert.IsNotNull(strType);

        var node = strType.From("hello");
        Assert.IsNotNull(node);
        Assert.AreEqual("hello", node.GetValue<string>());
    }

    [TestMethod]
    public async Task ScalarNode_BoolType()
    {
        var boolType = await Context.GetNodeTypeAsync<ScalarType>(NS_SYSTEM_BOOL);
        Assert.IsNotNull(boolType);

        var node = boolType.From(true);
        Assert.IsNotNull(node);
        Assert.AreEqual(true, node.GetValue<bool>());
    }
}
