using SchemaNode.Node;
using SchemaNode.Property.Struct;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Schema.Provider;
using static SchemaNode.Utility.Constant;
using StructType = SchemaNode.Runtime.StructType;

namespace SchemaNode.UnitTest.Core;

/// <summary>
/// Regression coverage for the type-definition row editor: an empty optional composite
/// (an untouched description or init) must not surface the required constraints of its
/// internal fields, and branches hidden by the definition kind must not block the row.
/// </summary>
[TestClass]
public class StructFieldRowValidationTest : Base.AppTestBase
{
    [TestMethod]
    public async Task FieldRows_WithEmptyOptionalComposites_CanBeValidatedAndSaved()
    {
        var node = await CreateDefinitionAsync("system", "minmax");

        var fields = node.GetAccessValue("struct.fields") as ArrayNode;
        Assert.IsNotNull(fields, "struct.fields was not materialized");

        AddFieldRow(fields, "min");
        AddFieldRow(fields, "max");

        await ValidateTreeAsync(node);

        Assert.IsTrue(node.IsValid, FormatErrors(node));
        Assert.IsTrue(((DataNode)fields.ElementAt(0)).IsValid, FormatErrors((DataNode)fields.ElementAt(0)));
        Assert.IsTrue(((DataNode)fields.ElementAt(1)).IsValid, FormatErrors((DataNode)fields.ElementAt(1)));

        // The definition must round-trip through the editor read-back and the storage provider.
        NodeSchema? schema = node.GetValue<NodeSchema>();
        Assert.IsNotNull(schema);
        Assert.IsTrue(await Context.SaveSchemaAsync(schema));

        var saved = await Context.GetNodeTypeAsync<StructType>(schema.FullName);
        Assert.IsNotNull(saved);
        Assert.AreEqual(2, saved.GetFields().Count());
        Assert.IsTrue(saved.GetFields().Any(field => field.Name == "min"));
        Assert.IsTrue(saved.GetFields().Any(field => field.Name == "max"));
    }

    [TestMethod]
    public async Task FieldRow_WithoutName_RemainsInvalid()
    {
        var node = await CreateDefinitionAsync("system", "noname");

        var fields = node.GetAccessValue("struct.fields") as ArrayNode;
        Assert.IsNotNull(fields);

        AddFieldRow(fields, name: null);

        await ValidateTreeAsync(node);

        Assert.IsFalse(((DataNode)fields.ElementAt(0)).IsValid, "a visible required name must still block the row");
    }

    private async Task<StructNode> CreateDefinitionAsync(string @namespace, string name)
    {
        Context.FullMode = true;

        var definitionType = await Context.GetNodeTypeAsync<StructType>("system.schema.node.schema");
        Assert.IsNotNull(definitionType);

        var node = definitionType.Create(Context) as StructNode;
        Assert.IsNotNull(node);

        node["namespace"] = @namespace;
        node["name"] = $"{@namespace}.{name}";
        node["kind"] = NODE_KIND_STRUCT;
        return node;
    }

    private void AddFieldRow(ArrayNode fields, string? name)
    {
        var element = fields.ElementType.Create(Context) as StructNode;
        Assert.IsNotNull(element);

        if (name != null) element["name"] = name;
        element["type"] = NS_SYSTEM_INT;
        fields.AddRow(null, element);
    }

    /// <summary>
    /// Mirrors <c>RowEditorValidation</c>: walks the visible tree and validates every node,
    /// because <see cref="DataNode.IsValid"/> only reports the last recorded violations.
    /// </summary>
    private static async Task ValidateTreeAsync(DataNode node)
    {
        await node.ValidateAsync(CancellationToken.None);

        if (node is ArrayNode array)
        {
            foreach (IValueAccess item in array)
                if (item is DataNode child) await ValidateTreeAsync(child);
            return;
        }

        if (node is StructNode structure)
        {
            foreach ((StructFieldType _, IValueAccess value) in structure.GetFields())
                if (value is DataNode child) await ValidateTreeAsync(child);
        }
    }

    private static string FormatErrors(DataNode node)
        => "Errors: " + string.Join(" | ", node.GetErrorNodes().Select(e => $"{e.Name} ({e.Type.Name})"));
}
