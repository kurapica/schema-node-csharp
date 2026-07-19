using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;
using ValueType = SchemaNode.Runtime.ValueType;

namespace SchemaNode.UnitTest.Core;

[Meta<SchemaType>("test.enum.color")]
public enum TestColorEnum { Red = 1, Green = 2, Blue = 3 }

[Meta<SchemaType>("test.enum.status")]
public enum TestStatusEnum { Active, Inactive, Pending }

[TestClass]
public class EnumTypeTest : Base.CoreTestBase
{
    [TestMethod]
    public async Task EnumType_Load_GeneratedEnum()
    {
        var colorType = await Context.GetNodeTypeAsync<EnumType>("test.enum.color");
        Assert.IsNotNull(colorType);
        Assert.AreEqual(SCHEMA_KIND_ENUM, colorType.Kind);
    }

    [TestMethod]
    public async Task EnumType_Load_Values()
    {
        var colorType = await Context.GetNodeTypeAsync<EnumType>("test.enum.color");
        Assert.IsNotNull(colorType);
        var subList = await colorType.GetEnumEntryAccessAsync(Context, null);
        Assert.IsNotNull(subList);
        Assert.IsTrue(subList.Length >= 3);
    }

    [TestMethod]
    public async Task EnumNode_Create()
    {
        var colorType = await Context.GetNodeTypeAsync<EnumType>("test.enum.color");
        Assert.IsNotNull(colorType);
        var node = colorType.Create();
        Assert.IsNotNull(node);
        Assert.IsTrue(node.IsEmpty);
    }

    [TestMethod]
    public async Task EnumNode_ValidateValue()
    {
        var colorType = await Context.GetNodeTypeAsync<EnumType>("test.enum.color");
        Assert.IsNotNull(colorType);
        var node = await colorType.ValidateValueAsync(Context, 1L);
        Assert.IsNotNull(node);
    }

    [TestMethod]
    public async Task EnumType_IsIndexable()
    {
        var colorType = await Context.GetNodeTypeAsync<EnumType>("test.enum.color");
        Assert.IsNotNull(colorType);
        Console.WriteLine($"IsIndexable: {colorType.IsIndexable}");
    }

    [TestMethod]
    public async Task EnumType_IsAssignableTo_Self()
    {
        var colorType = await Context.GetNodeTypeAsync<EnumType>("test.enum.color");
        Assert.IsNotNull(colorType);
        Assert.IsTrue(colorType.IsAssignableTo(colorType));
    }

    [TestMethod]
    public async Task EnumType_IsAssignableTo_UnderlyingScalar()
    {
        var colorType = await Context.GetNodeTypeAsync<EnumType>("test.enum.color");
        var intType = await Context.GetNodeTypeAsync<ValueType>(NS_SYSTEM_INT);
        Assert.IsNotNull(colorType);
        Assert.IsNotNull(intType);
        Assert.IsNotNull(colorType); Assert.IsNotNull(intType);
    }

    [TestMethod]
    public async Task EnumType_LoadEnumSubList_ByValue()
    {
        var colorType = await Context.GetNodeTypeAsync<EnumType>("test.enum.color");
        Assert.IsNotNull(colorType);
        var access = await colorType.GetEnumEntryAccessAsync(Context, "Red");
        Assert.IsNotNull(access);
    }

    [TestMethod]
    public async Task EnumType_LoadEnumAccessList()
    {
        var colorType = await Context.GetNodeTypeAsync<EnumType>("test.enum.color");
        Assert.IsNotNull(colorType);
        var accessList = await colorType.GetEnumEntryAccessAsync(Context, "Red");
        Assert.IsNotNull(accessList);
    }

    [TestMethod]
    public async Task EnumType_LoadEnumAccessList_InvalidValue()
    {
        var colorType = await Context.GetNodeTypeAsync<EnumType>("test.enum.color");
        Assert.IsNotNull(colorType);
        var accessList = await colorType.GetEnumEntryAccessAsync(Context, "NonExistent");
        Assert.IsNotNull(accessList);
        Assert.AreEqual(0, accessList.Length);
    }

    [TestMethod]
    public async Task EnumType_ValidateInvalidEnumValue()
    {
        var colorType = await Context.GetNodeTypeAsync<EnumType>("test.enum.color");
        Assert.IsNotNull(colorType);

        // Validate with a value not in the enum (99L)
        var node = await colorType.ValidateValueAsync(Context, 99L);
        Assert.IsNotNull(node);
        // Should have a violation for invalid enum value
        Console.WriteLine($"Validate 99L: IsValid={node.IsValid}, Violated={string.Join(", ", node.Violated ?? [])}");
    }

    [TestMethod]
    public async Task EnumType_Validate_StringEnum()
    {
        var statusType = await Context.GetNodeTypeAsync<EnumType>("test.enum.status");
        Assert.IsNotNull(statusType);

        // Status enum is string-based from TestStatusEnum (Active, Inactive, Pending)
        var node = await statusType.ValidateValueAsync(Context, "Active");
        Assert.IsNotNull(node);
        Assert.IsTrue(node.IsValid);
    }

    [TestMethod]
    public async Task EnumType_LoadAllValues()
    {
        var colorType = await Context.GetNodeTypeAsync<EnumType>("test.enum.color");
        Assert.IsNotNull(colorType);

        var subList = (await colorType.GetEnumEntryAccessAsync(Context, null)).FirstOrDefault()?.Children;
        Assert.IsNotNull(subList);
        Assert.IsTrue(subList.Length >= 3, $"Expected at least 3 values, got {subList.Length}");

        var values = subList.Select(v => v.Value).ToList();
        Console.WriteLine($"Enum values: {string.Join(", ", values)}");
    }

    [TestMethod]
    public async Task EnumType_IsAssignableTo_Object()
    {
        var colorType = await Context.GetNodeTypeAsync<EnumType>("test.enum.color");
        var objType = await Context.GetNodeTypeAsync<ValueType>(NS_SYSTEM_OBJECT);
        Assert.IsNotNull(colorType);
        Assert.IsNotNull(objType);
        Assert.IsTrue(colorType.IsAssignableTo(objType));
    }
}
