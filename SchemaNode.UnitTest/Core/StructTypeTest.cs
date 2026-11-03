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
        Assert.AreEqual(NODE_KIND_STRUCT, contextType.Kind);
    }

    [TestMethod]
    public async Task StructNode_Create()
    {
        var contextType = await Context.GetNodeTypeAsync<StructType>(NS_SYSTEM_CONTEXT);
        Assert.IsNotNull(contextType);

        var node = contextType.Create(Context) as StructNode;
        Assert.IsNotNull(node);
        Assert.IsTrue(node.IsEmpty);
    }

    [TestMethod]
    public async Task StructType_Load_GeneratedStruct()
    {
        var personType = await Context.GetNodeTypeAsync<StructType>("test.generator.person");
        Assert.IsNotNull(personType);
        Assert.AreEqual(NODE_KIND_STRUCT, personType.Kind);
    }

    [TestMethod]
    public async Task StructNode_GeneratedFields()
    {
        var personType = await Context.GetNodeTypeAsync<StructType>("test.generator.person");
        Assert.IsNotNull(personType);

        var node = personType.Create(Context) as StructNode;
        Assert.IsNotNull(node);

        var nameField = node.GetAccessValue("Name");
        Assert.IsNotNull(nameField);
    }

    [TestMethod]
    public async Task StructNode_SetAndGetField()
    {
        var personType = await Context.GetNodeTypeAsync<StructType>("test.generator.person");
        Assert.IsNotNull(personType);

        var node = personType.Create(Context) as StructNode;
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

        var node = personType.Create(Context) as StructNode;
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

        var original = personType.Create(Context) as StructNode;
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

        var node = personType.Create(Context) as StructNode;
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
    public async Task StructType_Load_AttachMultipleKinds()
    {
        var attachedType = await Context.GetNodeTypeAsync<StructType>("test.generator.attached");
        Assert.IsNotNull(attachedType);

        var fields = attachedType.GetFields().ToArray();

        // The fields from both attached kinds must be merged without duplicates
        Assert.AreEqual(fields.Length, fields.Select(f => f.Name.ToLowerInvariant()).Distinct().Count());
        Assert.IsTrue(fields.Any(f => f.Name.Equals("valid", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(fields.Any(f => f.Name.Equals("unit", StringComparison.OrdinalIgnoreCase)));

        // "lowlimit"/"uplimit" are declared by both attached kinds. They must be mounted
        // once, and the first kind (node.int.define) keeps ownership, so the field type
        // stays system.int instead of being overwritten by node.string.define.
        Assert.AreEqual(1, fields.Count(f => f.Name.Equals("lowlimit", StringComparison.OrdinalIgnoreCase)));
        Assert.AreEqual(1, fields.Count(f => f.Name.Equals("uplimit", StringComparison.OrdinalIgnoreCase)));
        Assert.AreEqual(NS_SYSTEM_INT,
            fields.Single(f => f.Name.Equals("lowlimit", StringComparison.OrdinalIgnoreCase)).Type?.Name);

        // Declared fields come first, attach fields follow the TS priority order
        // (getAttachPropertyPriority from struct/runtime.ts, priority desc, then name):
        //   csregex      string                      7000 + 3 forSchemas            = 7003
        //   error/unit   localestring struct        3000 + STRUCT_TYPE_PRIORITY 900 = 3900 (struct: no forSchemas)
        //   entrySource  funccall<entrysource>      3000 + 5 fields * -10          = 2950
        //   valid        funccall<valid>            3000 + 5 fields * -10          = 2950
        //   lowlimit/uplimit int has no VALUE_TYPE_PRIORITY entry, so only forSchemas = 3
        // Same priority breaks by name, the first property schema keeps a shared field name
        // (int.lowlimit wins over string.lowlimit because node.int.define comes first).
        string[] expected = ["name", "csregex", "error", "unit", "entrysource", "valid", "lowlimit", "uplimit"];
        string[] actual = fields.Select(f => f.Name.ToLowerInvariant()).ToArray();
        Assert.AreEqual(expected.Length, actual.Length,
            $"length mismatch, actual order: {string.Join(",", fields.Select(f => $"{f.Name}({f.Type?.Name})"))}");
        for (int i = 0; i < expected.Length; i++)
            Assert.AreEqual(expected[i], actual[i],
                $"index {i}, actual order: {string.Join(",", fields.Select(f => $"{f.Name}({f.Type?.Name})"))}");
    }

    [TestMethod]
    public async Task StructType_Load_SystemContext_Fields()
    {
        var contextType = await Context.GetNodeTypeAsync<StructType>(NS_SYSTEM_CONTEXT);
        Assert.IsNotNull(contextType);

        var node = contextType.Create(Context) as StructNode;
        Assert.IsNotNull(node);

        // system.context has fields like Target, Items etc.
        var targetField = node!.GetAccessValue("Target");
        Console.WriteLine($"system.context Target field: {targetField?.GetType().Name ?? "null"}");
    }
}
