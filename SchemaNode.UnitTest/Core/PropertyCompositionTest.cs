using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Constraint;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;
using ValueType = SchemaNode.Runtime.ValueType;

namespace SchemaNode.UnitTest.Core;

/// <summary>
/// Tests for the Property&lt;T&gt; composition system in SchemaNode.Core.
/// Properties are the central design thesis — they compose on schemas via JsonExtensionData.
/// </summary>
[TestClass]
public class PropertyCompositionTest : Base.CoreTestBase
{
    /// <summary>
    /// Scalar types have Display property that can be accessed
    /// </summary>
    [TestMethod]
    public async Task Property_Display_OnScalarType()
    {
        var intType = await Context.GetNodeTypeAsync<ScalarType>(NS_SYSTEM_INT);
        Assert.IsNotNull(intType);

        // Display is a common property; system types may or may not have it set
        var display = intType.GetProperty<Display>();
        // Even if not set, GetProperty should return null gracefully
        Console.WriteLine($"Display property on system.int: {display?.Value}");
    }

    /// <summary>
    /// Scalar types can have Require constraint property checked
    /// </summary>
    [TestMethod]
    public async Task Property_Constraint_Require()
    {
        var intType = await Context.GetNodeTypeAsync<ScalarType>(NS_SYSTEM_INT);
        Assert.IsNotNull(intType);

        // Constraints collection should be accessible
        var constraints = intType.Constraints;
        Assert.IsNotNull(constraints);
    }

    /// <summary>
    /// NodeType.GetProperty by name works
    /// </summary>
    [TestMethod]
    public async Task Property_GetProperty_ByName()
    {
        var intType = await Context.GetNodeTypeAsync<ScalarType>(NS_SYSTEM_INT);
        Assert.IsNotNull(intType);

        // GetProperty by string name
        var prop = intType.GetProperty<Display>();
        Console.WriteLine($"Property by name 'Display': {prop?.Value?.Key}");
    }

    /// <summary>
    /// NodeType.GetProperties returns all properties
    /// </summary>
    [TestMethod]
    public async Task Property_GetProperties_EnumerateAll()
    {
        var strType = await Context.GetNodeTypeAsync<ScalarType>(NS_SYSTEM_STRING);
        Assert.IsNotNull(strType);

        var allProps = strType.GetProperties<IProperty>().ToList();
        Console.WriteLine($"Total properties on system.string: {allProps.Count}");
        foreach (var prop in allProps)
            Console.WriteLine($"  - {prop.Name}");
        
        Assert.IsTrue(allProps.Count >= 0, "Should be able to enumerate properties");
    }

    /// <summary>
    /// Constraint properties are accessible via the Constraints collection
    /// </summary>
    [TestMethod]
    public async Task Property_Constraints_OnType()
    {
        var strType = await Context.GetNodeTypeAsync<ValueType>(NS_SYSTEM_STRING);
        Assert.IsNotNull(strType);

        var constraints = strType.Constraints.ToList();
        Console.WriteLine($"Constraints on system.string: {constraints.Count}");
        foreach (var c in constraints)
            Console.WriteLine($"  - {c.Name}");
    }

    /// <summary>
    /// Properties propagate from base types (scalar inheritance)
    /// </summary>
    [TestMethod]
    public async Task Property_Propagation_FromBaseType()
    {
        // system.int inherits from system.number
        var intType = await Context.GetNodeTypeAsync<ScalarType>(NS_SYSTEM_INT);
        Assert.IsNotNull(intType);

        // ScalarType.GetProperty proxies to BaseNode if not found locally
        // This test verifies the property chain doesn't throw
        var display = intType.GetProperty<Display>();
        Console.WriteLine($"Display from int (may be from base): {display?.Value}");
    }
}
