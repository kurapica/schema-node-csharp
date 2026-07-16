using System.Text.Json.Nodes;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Property.Constraint;
using SchemaNode.Schema;
using SchemaNode.Schema.Provider;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.UnitTest.App;

[TestClass]
public class CustomStructTest : Base.AppTestBase
{
    [TestMethod]
    public async Task CustomStruct_NewStruct()
    {
        NodeSchema schema = new NodeSchema
        {
            Namespace = "system",
            Name = "address",
            Kind = SCHEMA_KIND_STRUCT,
        };
        StructSchema addressSchema = new StructSchema
        {
            Fields = [
                new StructFieldSchema
                {
                    Name = "street",
                    Type = NS_SYSTEM_STRING,
                },
                new StructFieldSchema
                {
                    Name = "city",
                    Type = NS_SYSTEM_STRING,
                },
                new StructFieldSchema
                {
                    Name = "zipCode",
                    Type = NS_SYSTEM_INT,
                }
            ]
        };
        addressSchema.Fields[0].SetProperty<UpLimitString, long>(20);
        addressSchema.Fields[1].SetProperty<UpLimitString, long>(20);
        addressSchema.Fields[2].SetProperty<UpLimitInt, long>(99999);
        schema.SetProperty<StructProperty, StructSchema>(addressSchema);
        bool result = await Context.SaveSchemaAsync(schema);
        Assert.IsTrue(result);
        
        // Type
        var addressType = await Context.GetNodeTypeAsync<Runtime.StructType>(schema.FullName);
        Assert.IsNotNull(addressType);
        
        // Data
        var addressData = new JsonObject
        {
            ["street"] = "123 Main St",
            ["city"] = "Anytown",
            ["zipCode"] = 1234523123
        };
        var data = await addressType.ValidateValueAsync(Context, addressData);
        Assert.IsNotNull(data);
        Assert.IsFalse(data.IsValid);
        
        var zipCode = data.GetAccessValue("zipCode") as DataNode;
        Assert.IsNotNull(zipCode);
        Assert.IsFalse(zipCode.IsValid);
        Assert.AreEqual("uplimit", zipCode.Violated?.ElementAtOrDefault(0));
    }

    [TestMethod]
    public async Task CustomStruct_WithDateAndBool()
    {
        NodeSchema schema = new NodeSchema
        {
            Namespace = "system",
            Name = "event_info",
            Kind = SCHEMA_KIND_STRUCT,
        };
        StructSchema eventSchema = new StructSchema
        {
            Fields = [
                new StructFieldSchema { Name = "title", Type = NS_SYSTEM_STRING },
                new StructFieldSchema { Name = "eventDate", Type = NS_SYSTEM_DATE },
                new StructFieldSchema { Name = "isActive", Type = NS_SYSTEM_BOOL }
            ]
        };
        eventSchema.Fields[0].SetProperty<UpLimitString, long>(100);
        schema.SetProperty<StructProperty, StructSchema>(eventSchema);
        bool result = await Context.SaveSchemaAsync(schema);
        Assert.IsTrue(result);

        var eventType = await Context.GetNodeTypeAsync<Runtime.StructType>(schema.FullName);
        Assert.IsNotNull(eventType);

        var data = new JsonObject
        {
            ["title"] = "Conference",
            ["eventDate"] = "2025-06-15T00:00:00Z",
            ["isActive"] = true
        };
        var node = await eventType.ValidateValueAsync(Context, data);
        Assert.IsNotNull(node);
        Assert.IsTrue(node.IsValid);

        var title = node.GetAccessValue("title");
        Assert.IsNotNull(title);
        Assert.AreEqual("Conference", (title as DataNode)?.GetValue<string>());
    }

    [TestMethod]
    public async Task CustomStruct_ValidateInvalidField()
    {
        NodeSchema schema = new NodeSchema
        {
            Namespace = "system",
            Name = "profile",
            Kind = SCHEMA_KIND_STRUCT,
        };
        StructSchema profileSchema = new StructSchema
        {
            Fields = [
                new StructFieldSchema { Name = "username", Type = NS_SYSTEM_STRING },
                new StructFieldSchema { Name = "age", Type = NS_SYSTEM_INT }
            ]
        };
        profileSchema.Fields[0].SetProperty<UpLimitString, long>(10);
        profileSchema.Fields[1].SetProperty<LowLimitInt, long>(0);
        profileSchema.Fields[1].SetProperty<UpLimitInt, long>(150);
        schema.SetProperty<StructProperty, StructSchema>(profileSchema);
        await Context.SaveSchemaAsync(schema);

        var profileType = await Context.GetNodeTypeAsync<Runtime.StructType>(schema.FullName);
        Assert.IsNotNull(profileType);

        // Username too long
        var data = new JsonObject
        {
            ["username"] = "this_username_is_way_too_long",
            ["age"] = 25
        };
        var node = await profileType.ValidateValueAsync(Context, data);
        Assert.IsNotNull(node);
        Assert.IsFalse(node.IsValid);

        var usernameField = node.GetAccessValue("username") as DataNode;
        Assert.IsNotNull(usernameField);
        Assert.IsFalse(usernameField.IsValid);
    }

    [TestMethod]
    public async Task CustomStruct_DeleteStruct()
    {
        NodeSchema schema = new NodeSchema
        {
            Namespace = "system",
            Name = "todelete_struct",
            Kind = SCHEMA_KIND_STRUCT,
        };
        StructSchema deleteSchema = new StructSchema
        {
            Fields = [new StructFieldSchema { Name = "x", Type = NS_SYSTEM_INT }]
        };
        schema.SetProperty<StructProperty, StructSchema>(deleteSchema);
        await Context.SaveSchemaAsync(schema);

        bool deleted = await Context.DeleteSchemaAsync(schema.FullName);
        Assert.IsTrue(deleted);
    }
}