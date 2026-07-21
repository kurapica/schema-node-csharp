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

        DataNode? node = await intType.ValidateValueAsync(Context, JsonValue.Create(10L)!);
        Assert.IsNotNull(node);
        Assert.AreEqual(10L, node.GetValue<long>());
        Assert.IsTrue(node.IsValid);
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

    [TestMethod]
    public async Task ScalarNode_DateType()
    {
        var dateType = await Context.GetNodeTypeAsync<ScalarType>(NS_SYSTEM_DATE);
        Assert.IsNotNull(dateType);

        var dt = DateTimeOffset.UtcNow;
        var node = dateType.From(dt);
        Assert.IsNotNull(node);
        Assert.AreEqual(dt, node.GetValue<DateTimeOffset>());
    }

    [TestMethod]
    public async Task ScalarNode_NumberType()
    {
        var numType = await Context.GetNodeTypeAsync<ScalarType>(NS_SYSTEM_NUMBER);
        Assert.IsNotNull(numType);

        var node = numType.From(3.14m);
        Assert.IsNotNull(node);
        Assert.AreEqual(3.14m, node.GetValue<decimal>());
    }

    [TestMethod]
    public async Task ScalarNode_Validate_InvalidType()
    {
        var intType = await Context.GetNodeTypeAsync<ScalarType>(NS_SYSTEM_INT);
        Assert.IsNotNull(intType);

        // Validate with string value for int type - should produce violations
        DataNode? node = await intType.ValidateValueAsync(Context, JsonValue.Create("not_a_number")!);
        Assert.IsNull(node);
    }

    [TestMethod]
    public async Task ScalarNode_Clone()
    {
        var intType = await Context.GetNodeTypeAsync<ScalarType>(NS_SYSTEM_INT);
        Assert.IsNotNull(intType);

        var original = intType.From(42L);
        Assert.IsNotNull(original);

        var cloned = original.Clone();
        Assert.IsNotNull(cloned);
        Assert.AreEqual(42L, cloned.GetValue<long>());
        Assert.AreNotSame(original, cloned);
    }

    [TestMethod]
    public async Task ScalarNode_ObjectType()
    {
        var objType = await Context.GetNodeTypeAsync<ScalarType>(NS_SYSTEM_OBJECT);
        Assert.IsNotNull(objType);

        var node = objType.From(42L);
        Assert.IsNotNull(node);
        Assert.AreEqual(42L, node.GetValue<long>());
    }

    [TestMethod]
    public async Task ScalarNode_ClearValue()
    {
        var intType = await Context.GetNodeTypeAsync<ScalarType>(NS_SYSTEM_INT);
        Assert.IsNotNull(intType);

        var node = intType.From(42L);
        Assert.IsFalse(node.IsEmpty);
        node.ClearValue();
        Assert.IsTrue(node.IsEmpty);
    }
}
