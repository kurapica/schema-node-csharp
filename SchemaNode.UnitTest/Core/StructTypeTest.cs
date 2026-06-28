using SchemaNode.Node;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.UnitTest.Core;

[TestClass]
public class StructTypeTest : Base.CoreTestBase
{
    [TestMethod]
    public async Task StructType_Load_SystemContext()
    {
        var contextType = await Context.GetNodeTypeAsync<StructType>(NS_SYSTEM_CONTEXT);
        Assert.IsNotNull(contextType);
        Assert.AreEqual(SCHEMA_KIND_STRUCT, contextType.Kind);
    }

    [TestMethod]
    public async Task StructNode_Create()
    {
        var contextType = await Context.GetNodeTypeAsync<StructType>(NS_SYSTEM_CONTEXT);
        Assert.IsNotNull(contextType);

        var node = contextType.Create() as StructNode;
        Assert.IsNotNull(node);
        Assert.IsTrue(node.IsEmpty);
    }

    [TestMethod]
    public async Task StructType_Load_GeneratedStruct()
    {
        var personType = await Context.GetNodeTypeAsync<StructType>("test.generator.person");
        Assert.IsNotNull(personType);
        Assert.AreEqual(SCHEMA_KIND_STRUCT, personType.Kind);
    }

    [TestMethod]
    public async Task StructNode_GeneratedFields()
    {
        var personType = await Context.GetNodeTypeAsync<StructType>("test.generator.person");
        Assert.IsNotNull(personType);

        var node = personType.Create() as StructNode;
        Assert.IsNotNull(node);

        var nameField = node.GetAccessValue("Name");
        Assert.IsNotNull(nameField);
    }

    [TestMethod]
    public async Task StructNode_SetAndGetField()
    {
        var personType = await Context.GetNodeTypeAsync<StructType>("test.generator.person");
        Assert.IsNotNull(personType);

        var node = personType.Create() as StructNode;
        Assert.IsNotNull(node);

        node["Name"] = "Alice";
        Assert.AreEqual("Alice", (node.GetAccessValue("Name") as DataNode)?.GetValue<string>());
    }
}
