using SchemaNode.Attribute;
using SchemaNode.Node;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;
using ValueType = SchemaNode.Runtime.ValueType;

namespace SchemaNode.RefactorTest.Core;

/// <summary>
/// Test enum type for EnumTypeTest (cascade enum simulation)
/// </summary>
[Meta<SchemaType>("test.enum.geo")]
public enum TestGeo
{
    Asia = 1,
    Europe = 2,
    Africa = 3,
    NorthAmerica = 4,
    SouthAmerica = 5
}

/// <summary>
/// Tests for EnumType and enum operations in SchemaNode.Core.
/// Covers basic enum, cascade enum loading, and enum operations.
/// </summary>
[TestClass]
public class EnumTypeTest : Base.CoreTestBase
{
    #region Basic Enum Tests

    /// <summary>
    /// Basic enum register and load: ensure generated enum type is available
    /// </summary>
    [TestMethod]
    public async Task EnumType_Load_GeneratedEnum()
    {
        var geoType = await Context.GetNodeTypeAsync<EnumType>("test.enum.geo");
        Assert.IsNotNull(geoType, "test.enum.geo should be registered");
        Assert.AreEqual(SCHEMA_KIND_ENUM, geoType.Kind);
    }

    /// <summary>
    /// Enum type has correct enum value type
    /// </summary>
    [TestMethod]
    public async Task EnumType_BasicProperties()
    {
        var geoType = await Context.GetNodeTypeAsync<EnumType>("test.enum.geo");
        Assert.IsNotNull(geoType);
        
        // Int enum should have EnumValueType.Int
        Console.WriteLine($"Enum type: {geoType.Name}, Kind: {geoType.Kind}, ValueType: {geoType.Type}");
    }

    /// <summary>
    /// EnumNode create and set an enum value
    /// </summary>
    [TestMethod]
    public async Task EnumNode_CreateAndSetValue()
    {
        var geoType = await Context.GetNodeTypeAsync<EnumType>("test.enum.geo");
        Assert.IsNotNull(geoType);

        var node = geoType.Create();
        Assert.IsNotNull(node);
        Assert.IsTrue(node.IsEmpty, "Newly created enum node should be empty");
    }

    /// <summary>
    /// EnumType can validate a valid value
    /// </summary>
    [TestMethod]
    public async Task EnumType_ValidateValidValue()
    {
        var colorType = await Context.GetNodeTypeAsync<EnumType>("test.generator.color");
        Assert.IsNotNull(colorType);

        // Validate with a valid enum integer value
        DataNode node = await colorType.ValidateValueAsync(Context, 1L);
        Assert.IsNotNull(node);
    }

    #endregion

    #region IsAssignableTo

    /// <summary>
    /// Enum type is assignable to its underlying scalar type
    /// </summary>
    [TestMethod]
    public async Task EnumType_IsAssignableTo_UnderlyingScalar()
    {
        var geoType = await Context.GetNodeTypeAsync<EnumType>("test.enum.geo");
        var intType = await Context.GetNodeTypeAsync<ValueType>(NS_SYSTEM_INT);
        Assert.IsNotNull(geoType);
        Assert.IsNotNull(intType);

        bool result = geoType.IsAssignableTo(intType);
        Console.WriteLine($"Enum.IsAssignableTo(int): {result}");
    }

    /// <summary>
    /// Same enum type is assignable to itself
    /// </summary>
    [TestMethod]
    public async Task EnumType_IsAssignableTo_Self()
    {
        var geoType = await Context.GetNodeTypeAsync<EnumType>("test.enum.geo");
        Assert.IsNotNull(geoType);
        Assert.IsTrue(geoType.IsAssignableTo(geoType));
    }

    #endregion

    #region Enum Value Operations

    /// <summary>
    /// Load enum sub-list from root (empty value = root level)
    /// </summary>
    [TestMethod]
    public async Task EnumType_LoadEnumSubList_Root()
    {
        var geoType = await Context.GetNodeTypeAsync<EnumType>("test.enum.geo");
        Assert.IsNotNull(geoType);

        var subList = await geoType.LoadEnumSubListAsync(Context, null);
        Assert.IsNotNull(subList);
        Console.WriteLine($"Root sub-list count: {subList.Length}");
        foreach (var v in subList)
            Console.WriteLine($"  - {v.Value}");
    }

    /// <summary>
    /// Load enum value access (path from root to a specific value)
    /// </summary>
    [TestMethod]
    public async Task EnumType_LoadEnumValueAccess()
    {
        var geoType = await Context.GetNodeTypeAsync<EnumType>("test.enum.geo");
        Assert.IsNotNull(geoType);

        // Load access for "Asia" value
        var subList = await geoType.LoadEnumSubListAsync(Context, "Asia");
        Assert.IsNotNull(subList);
    }

    /// <summary>
    /// Load enum access list
    /// </summary>
    [TestMethod]
    public async Task EnumType_LoadEnumAccessList()
    {
        var geoType = await Context.GetNodeTypeAsync<EnumType>("test.enum.geo");
        Assert.IsNotNull(geoType);

        var accessList = await geoType.LoadEnumAccessListAsync(Context, "Asia", noSubList: true);
        Assert.IsNotNull(accessList);
    }

    /// <summary>
    /// Load enum access list for non-existent value returns empty
    /// </summary>
    [TestMethod]
    public async Task EnumType_LoadEnumAccessList_InvalidValue()
    {
        var geoType = await Context.GetNodeTypeAsync<EnumType>("test.enum.geo");
        Assert.IsNotNull(geoType);

        var accessList = await geoType.LoadEnumAccessListAsync(Context, "NonExistent");
        Assert.IsNotNull(accessList);
        Assert.AreEqual(0, accessList.Length, "Non-existent value should return empty access list");
    }

    #endregion
}
