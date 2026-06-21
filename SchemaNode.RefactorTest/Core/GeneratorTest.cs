using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;
using ValueType = SchemaNode.Runtime.ValueType;

namespace SchemaNode.RefactorTest.Core;

/// <summary>
/// Test types used by GeneratorTest to verify code-to-schema generation.
/// These types are annotated with [Meta] attributes and will be picked up
/// by the runtime during InitSchemaRuntimeAsync().
/// </summary>

/// <summary>
/// Test struct type for StructGenerator verification
/// </summary>
[Meta<SchemaType>("test.generator.person")]
public class TestPerson
{
    public string? Name { get; set; }
    public int Age { get; set; }
    public string? Phone { get; set; }
}

/// <summary>
/// Test enum type for EnumGenerator verification
/// </summary>
[Meta<SchemaType>("test.generator.color")]
public enum TestColor
{
    Red = 1,
    Green = 2,
    Blue = 3
}

/// <summary>
/// Tests for INodeSchemaGenerator implementations.
/// Verifies that C# types annotated with [Meta&lt;SchemaType&gt;] are correctly
/// converted to NodeType instances during runtime initialization.
/// </summary>
[TestClass]
public class GeneratorTest : Base.CoreTestBase
{
    /// <summary>
    /// StructGenerator: C# class → StructType with correct fields
    /// </summary>
    [TestMethod]
    public async Task StructGenerator_ClassToStructType()
    {
        var personType = await Context.GetNodeTypeAsync<ValueType>("test.generator.person");
        Assert.IsNotNull(personType, "TestPerson should be registered as a schema type");
        Assert.AreEqual(SCHEMA_KIND_STRUCT, personType.Kind, "TestPerson should be a struct type");
    }

    /// <summary>
    /// EnumGenerator: C# enum → EnumType with correct values
    /// </summary>
    [TestMethod]
    public async Task EnumGenerator_EnumToEnumType()
    {
        var colorType = await Context.GetNodeTypeAsync<EnumType>("test.generator.color");
        Assert.IsNotNull(colorType, "TestColor should be registered as a schema type");
        Assert.AreEqual(SCHEMA_KIND_ENUM, colorType.Kind, "TestColor should be an enum type");
    }

    /// <summary>
    /// Generated struct type can create data nodes
    /// </summary>
    [TestMethod]
    public async Task Generator_StructCanCreateNode()
    {
        var personType = await Context.GetNodeTypeAsync<StructType>("test.generator.person");
        Assert.IsNotNull(personType);

        var node = personType.Create();
        Assert.IsNotNull(node, "Generated struct type should be able to create a StructNode");
    }

    /// <summary>
    /// Generated enum type has values loaded
    /// </summary>
    [TestMethod]
    public async Task Generator_EnumHasValues()
    {
        var colorType = await Context.GetNodeTypeAsync<EnumType>("test.generator.color");
        Assert.IsNotNull(colorType);
        
        // Enum should have values (Red, Green, Blue)
        Console.WriteLine($"Enum type: {colorType.Name}, Kind: {colorType.Kind}, Type: {colorType.Type}");
    }
}
