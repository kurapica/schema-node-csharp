using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Property.Constraint;
using SchemaNode.Schema;
using SchemaNode.Schema.Provider;
using static SchemaNode.Utility.Constant;
using IntType = SchemaNode.Runtime.IntType;

namespace SchemaNode.UnitTest.App;

[TestClass]
public class CustomScalarTest : Base.AppTestBase
{
    [TestMethod]
    public async Task CustomScalar_NewInt()
    {
        NodeSchema schema = new ()
        {
            Namespace = "system",
            Name = "age",
            Kind = SCHEMA_KIND_INT,
        };
        IntSchema ageSchema = new IntSchema
        {
            Base = NS_SYSTEM_INT,
        };
        ageSchema.SetProperty<LowLimitInt, long>(0);
        ageSchema.SetProperty<UplimitInt, long>(200);
        schema.SetProperty<IntProperty, IntSchema>(ageSchema);
        bool result = await Context.SaveSchemaAsync(schema);
        Assert.IsTrue(result);
        
        IntType? ageType = await Context.GetNodeTypeAsync<IntType>(schema.FullName);
        Assert.IsNotNull(ageType);

        DataNode? node = await ageType.ValidateValueAsync(Context, 250);
        Assert.IsNotNull(node);
        Assert.IsFalse(node.IsValid);
        Assert.IsTrue(node.Violated?.Length == 1);
        Assert.AreEqual("uplimit", node.Violated?.ElementAtOrDefault(0));
    }
}