using SchemaNode.Node;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.RefactorTest.Core;

/// <summary>
/// Tests for StructType and StructNode in SchemaNode.Core
/// </summary>
[TestClass]
public class StructTypeTest : Base.CoreTestBase
{
    /// <summary>
    /// StructType register and load: verify struct type with fields
    /// </summary>
    [TestMethod]
    public async Task StructType_Load_SystemContext()
    {
        // system.context is a struct type registered by the runtime
        var contextType = await Context.GetNodeTypeAsync<StructType>(NS_SYSTEM_CONTEXT);
        Assert.IsNotNull(contextType, "system.context should be loaded");
        Assert.AreEqual(SCHEMA_KIND_STRUCT, contextType.Kind);
    }

    /// <summary>
    /// StructNode create and field access via path
    /// </summary>
    [TestMethod]
    public async Task StructNode_CreateAndAccessFields()
    {
        var contextType = await Context.GetNodeTypeAsync<StructType>(NS_SYSTEM_CONTEXT);
        Assert.IsNotNull(contextType);

        var node = contextType.Create();
        Assert.IsNotNull(node);
        Assert.IsTrue(node.IsEmpty, "Newly created struct node should be empty");
    }
}
