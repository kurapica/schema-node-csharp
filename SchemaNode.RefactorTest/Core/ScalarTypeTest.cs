using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.RefactorTest.Core;

/// <summary>
/// Tests for system scalar types and custom scalar types in SchemaNode.Core
/// </summary>
[TestClass]
public class ScalarTypeTest : Base.CoreTestBase
{
    /// <summary>
    /// Verify that system scalar types (system.bool / system.int / system.string) are loaded correctly
    /// </summary>
    [TestMethod]
    public async Task SystemScalarTypes_AreLoaded()
    {
        var boolType = await Context.GetNodeTypeAsync<ScalarType>(NS_SYSTEM_BOOL);
        var intType  = await Context.GetNodeTypeAsync<ScalarType>(NS_SYSTEM_INT);
        var strType  = await Context.GetNodeTypeAsync<ScalarType>(NS_SYSTEM_STRING);

        Assert.IsNotNull(boolType, "system.bool should be loaded");
        Assert.IsNotNull(intType,  "system.int should be loaded");
        Assert.IsNotNull(strType,  "system.string should be loaded");

        Assert.AreEqual(SCHEMA_KIND_BOOL, boolType.Kind);
        Assert.AreEqual(SCHEMA_KIND_INT, intType.Kind);
        Assert.AreEqual(SCHEMA_KIND_STRING, strType.Kind);
    }

    /// <summary>
    /// Basic scalar node operations: create a node, set and get its value
    /// </summary>
    [TestMethod]
    public async Task ScalarNode_SetAndGetValue()
    {
        var intType = await Context.GetNodeTypeAsync<ScalarType>(NS_SYSTEM_INT);
        Assert.IsNotNull(intType);

        var node = intType.From(42L);
        Assert.IsNotNull(node);
        Assert.AreEqual(42L, node.GetValue<long>());
        Assert.IsFalse(node.IsEmpty, "Node should not be empty after setting value");
    }

    /// <summary>
    /// ScalarNode constraint validation: validate value against scalar type
    /// </summary>
    [TestMethod]
    public async Task ScalarNode_Validate_ValidValue()
    {
        var intType = await Context.GetNodeTypeAsync<ScalarType>(NS_SYSTEM_INT);
        Assert.IsNotNull(intType);

        DataNode node = await intType.ValidateValueAsync(Context, 10L);
        Assert.IsNotNull(node);
        Assert.AreEqual(10L, node.GetValue<long>());
        Assert.IsTrue(node.Violated is null or { IsEmpty: true }, "Valid int should have no violated constraints");
    }
}
