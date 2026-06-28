using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Schema;
using SchemaNode.Schema.Provider;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.UnitTest.App;

[TestClass]
public class CustomEnumTest : Base.AppTestBase
{
    [TestMethod]
    public async Task CusotomEnum_NewEnum()
    {
        NodeSchema schema = new NodeSchema
        {
            Namespace = "system",
            Name = "status",
            Kind = SCHEMA_KIND_ENUM,
        };
        EnumSchema enumSchema = new EnumSchema
        {
            Type = EnumValueType.String,
            Values =
            [
                new EnumValueSchema { Value = "active" },
                new EnumValueSchema { Value = "inactive" },
                new EnumValueSchema { Value = "pending" }
            ]
        };
        schema.SetProperty<EnumProperty, EnumSchema>(enumSchema);
        bool result = await Context.SaveSchemaAsync(schema);
        Assert.IsTrue(result);
        
        var statusType = await Context.GetNodeTypeAsync<Runtime.EnumType>(schema.FullName);
        Assert.IsNotNull(statusType);
        
        DataNode? node = await statusType.ValidateValueAsync(Context, "active");
        Assert.IsNotNull(node);
        Assert.IsTrue(node.IsValid);
    }

    [TestMethod]
    public async Task CustomEnum_NewCascadeEnum()
    {
        NodeSchema schema = new NodeSchema
        {
            Namespace = "system",
            Name = "region",
            Kind = SCHEMA_KIND_ENUM,
        };
        EnumSchema enumSchema = new EnumSchema
        {
            Type = EnumValueType.String,
            Cascade = [ "Country", "City" ],
            Values = [
                new EnumValueSchema{ Value = "Usa" },
                new EnumValueSchema{ Value = "China" }
            ]
        };
        schema.SetProperty<EnumProperty, EnumSchema>(enumSchema);
        bool result = await Context.SaveSchemaAsync(schema);
        Assert.IsTrue(result);
        
        // sub list
        bool saved = await Context.SaveEnumSubListAsync(schema.FullName, "China", [
            new EnumValueSchema{ Value = "Beijing" },
            new EnumValueSchema{ Value = "Shanghai" }
        ]);
        Assert.IsTrue(saved);
        
        // Get type
        var regionType = await Context.GetNodeTypeAsync<Runtime.EnumType>(schema.FullName);
        Assert.IsNotNull(regionType);
        
        // access list
        var access = await regionType.LoadEnumAccessListAsync(Context, "Beijing");
        Assert.IsNotNull(access);
        Assert.AreEqual(2, access.Length);
    }
}