using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Schema;
using SchemaNode.Schema.Provider;
using SchemaNode.Struct;
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
                new Entry<string> { Value = "active" },
                new Entry<string> { Value = "inactive" },
                new Entry<string> { Value = "pending" }
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
                new Entry<string>{ Value = "Usa" },
                new Entry<string>{ Value = "China" }
            ]
        };
        schema.SetProperty<EnumProperty, EnumSchema>(enumSchema);
        bool result = await Context.SaveSchemaAsync(schema);
        Assert.IsTrue(result);
        
        // sub list
        bool saved = await Context.SaveEnumEntriesAsync(schema.FullName, "China", [
            new Entry<string>{ Value = "Beijing" },
            new Entry<string>{ Value = "Shanghai" }
        ]);
        Assert.IsTrue(saved);
        
        // Get type
        var regionType = await Context.GetNodeTypeAsync<Runtime.EnumType>(schema.FullName);
        Assert.IsNotNull(regionType);
        
        // access list
        var access = await regionType.GetEnumEntryAccessAsync(Context, "Beijing");
        Assert.IsNotNull(access);
        Assert.AreEqual(3, access.Length);
    }

    [TestMethod]
    public async Task CustomEnum_IntTypeEnum()
    {
        NodeSchema schema = new NodeSchema
        {
            Namespace = "system",
            Name = "priority",
            Kind = SCHEMA_KIND_ENUM,
        };
        EnumSchema enumSchema = new EnumSchema
        {
            Type = EnumValueType.Int,
            Values =
            [
                new Entry<string>() { Value = "1" },
                new Entry<string>() { Value = "2" },
                new Entry<string>() { Value = "3" }
            ]
        };
        schema.SetProperty<EnumProperty, EnumSchema>(enumSchema);
        bool result = await Context.SaveSchemaAsync(schema);
        Assert.IsTrue(result);

        var priorityType = await Context.GetNodeTypeAsync<Runtime.EnumType>(schema.FullName);
        Assert.IsNotNull(priorityType);

        var node = await priorityType.ValidateValueAsync(Context, 2L);
        Assert.IsNotNull(node);
        Assert.IsTrue(node.IsValid);
    }

    [TestMethod]
    public async Task CustomEnum_CascadeEnumWithAppend()
    {
        NodeSchema schema = new NodeSchema
        {
            Namespace = "system",
            Name = "continent",
            Kind = SCHEMA_KIND_ENUM,
        };
        EnumSchema enumSchema = new EnumSchema
        {
            Type = EnumValueType.String,
            Cascade = ["Continent", "Country"],
            Values =
            [
                new Entry<string>() { Value = "Asia" },
                new Entry<string>() { Value = "Europe" }
            ]
        };
        schema.SetProperty<EnumProperty, EnumSchema>(enumSchema);
        await Context.SaveSchemaAsync(schema);

        // Save sub-list for Asia with all countries at once
        await Context.SaveEnumEntriesAsync(schema.FullName, "Asia", [
            new Entry<string>() { Value = "Japan" },
            new Entry<string>() { Value = "Korea" },
            new Entry<string>() { Value = "India" }
        ]);

        var continentType = await Context.GetNodeTypeAsync<Runtime.EnumType>(schema.FullName);
        Assert.IsNotNull(continentType);

        // Load sub-list directly (not access list)
        var subList = (await continentType.GetEnumEntryAccessAsync(Context, "Asia")).Last().Children;
        Assert.IsNotNull(subList);
        Assert.IsTrue(subList.Length >= 3, $"Expected >= 3 sub-list values, got {subList.Length}");
    }

    [TestMethod]
    public async Task CustomEnum_DeleteEnum()
    {
        NodeSchema schema = new NodeSchema
        {
            Namespace = "system",
            Name = "todelete_enum",
            Kind = SCHEMA_KIND_ENUM,
        };
        EnumSchema enumSchema = new EnumSchema
        {
            Type = EnumValueType.String,
            Values = [new Entry<string>() { Value = "x" }]
        };
        schema.SetProperty<EnumProperty, EnumSchema>(enumSchema);
        await Context.SaveSchemaAsync(schema);

        bool deleted = await Context.DeleteSchemaAsync(schema.FullName);
        Assert.IsTrue(deleted);
    }
}