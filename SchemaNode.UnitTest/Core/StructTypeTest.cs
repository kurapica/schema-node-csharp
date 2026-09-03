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
        Assert.AreEqual("Alice", node.GetAccessValue("Name")?.GetValue<string>());
    }

    [TestMethod]
    public async Task StructNode_ValidateData()
    {
        var personType = await Context.GetNodeTypeAsync<StructType>("test.generator.person");
        Assert.IsNotNull(personType);

        var data = new System.Text.Json.Nodes.JsonObject
        {
            ["Name"] = "Bob",
            ["Age"] = 30,
            ["Phone"] = "1234567890"
        };
        var node = await personType.ValidateValueAsync(Context, data);
        Assert.IsNotNull(node);
        Assert.IsTrue(node.IsValid);
    }

    [TestMethod]
    public async Task StructNode_GetFieldValueAsync()
    {
        var personType = await Context.GetNodeTypeAsync<StructType>("test.generator.person");
        Assert.IsNotNull(personType);

        var node = personType.Create() as StructNode;
        Assert.IsNotNull(node);
        node!["Name"] = "Charlie";

        var fieldValue = personType.GetField("Name");
        Assert.IsNotNull(fieldValue, "GetField should return field metadata");
        Console.WriteLine($"Field Name: {fieldValue.Name}, Field Type: {fieldValue.Type}");
    }

    [TestMethod]
    public async Task StructNode_Clone()
    {
        var personType = await Context.GetNodeTypeAsync<StructType>("test.generator.person");
        Assert.IsNotNull(personType);

        var original = personType.Create() as StructNode;
        Assert.IsNotNull(original);
        original!["Name"] = "Diana";
        original["Age"] = 25;

        var cloned = original.Clone() as StructNode;
        Assert.IsNotNull(cloned);
        Assert.AreEqual("Diana", cloned!.GetAccessValue("Name")?.GetValue<string>());
        Assert.AreEqual(25, cloned.GetAccessValue("Age")?.GetValue<int>());
        Assert.AreNotSame(original, cloned);
    }

    [TestMethod]
    public async Task StructNode_MultipleFields()
    {
        var personType = await Context.GetNodeTypeAsync<StructType>("test.generator.person");
        Assert.IsNotNull(personType);

        var node = personType.Create() as StructNode;
        Assert.IsNotNull(node);

        // Set multiple fields
        node!["Name"] = "Eve";
        node["Age"] = 28;
        node["Phone"] = "555-0100";

        Assert.AreEqual("Eve", node.GetAccessValue("Name")?.GetValue<string>());
        Assert.AreEqual(28, node.GetAccessValue("Age")?.GetValue<int>());
        Assert.AreEqual("555-0100", node.GetAccessValue("Phone")?.GetValue<string>());
    }

    [TestMethod]
    public async Task StructType_Load_SystemContext_Fields()
    {
        var contextType = await Context.GetNodeTypeAsync<StructType>(NS_SYSTEM_CONTEXT);
        Assert.IsNotNull(contextType);

        var node = contextType.Create() as StructNode;
        Assert.IsNotNull(node);

        // system.context has fields like Target, Items etc.
        var targetField = node!.GetAccessValue("Target");
        Console.WriteLine($"system.context Target field: {targetField?.GetType().Name ?? "null"}");
    }
}
