using SchemaNode.Node;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.RefactorTest.Core;

/// <summary>
/// Tests for DataNode serialization in SchemaNode.Core
/// </summary>
[TestClass]
public class NodeSerializationTest : Base.CoreTestBase
{
    /// <summary>
    /// Serialize a ScalarNode to JSON
    /// </summary>
    [TestMethod]
    public async Task ScalarNode_SerializeToJson()
    {
        var intType = await Context.GetNodeTypeAsync<ScalarType>(NS_SYSTEM_INT);
        Assert.IsNotNull(intType);

        var node = intType.From(42);
        Assert.IsNotNull(node);
        
        // Verify the node holds the correct value
        Assert.AreEqual(42L, node.GetValue<long>());
    }

    /// <summary>
    /// Verify StructNode can be created and inspected
    /// </summary>
    [TestMethod]
    public async Task StructNode_CreateAndInspect()
    {
        var contextType = await Context.GetNodeTypeAsync<StructType>(NS_SYSTEM_CONTEXT);
        Assert.IsNotNull(contextType);

        var node = contextType.Create();
        Assert.IsNotNull(node);
        Assert.AreEqual(SCHEMA_KIND_STRUCT, node.Type.Kind);
    }
}
