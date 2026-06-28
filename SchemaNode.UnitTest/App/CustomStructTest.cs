using System.Text.Json.Nodes;
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
        addressSchema.Fields[0].SetProperty<UplimitString, long>(20);
        addressSchema.Fields[1].SetProperty<UplimitString, long>(20);
        addressSchema.Fields[2].SetProperty<UplimitInt, long>(99999);
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
        
        var zipCode = data.GetAccessValue("zipCode");
        Assert.IsNotNull(zipCode);
        Assert.IsFalse(zipCode.IsValid);
        Assert.AreEqual("uplimit", zipCode.Violated?.ElementAtOrDefault(0));
    }
}