using SchemaNode.Property.Int;
using SchemaNode.Property.String;
using SchemaNode.Runtime;
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
        ageSchema.SetProperty<UpLimitInt, long>(200);
        schema.SetProperty<IntProperty, IntSchema>(ageSchema);
        bool result = await Context.SaveSchemaAsync(schema);
        Assert.IsTrue(result);
        
        IntType? ageType = await Context.GetNodeTypeAsync<IntType>(schema.FullName);
        Assert.IsNotNull(ageType);

        var node = await ageType.ValidateValueAsync(Context, 250);
        Assert.IsNotNull(node);
        Assert.IsFalse(node.IsValid);
        var violated = node.GetViolatedConstraints().ToArray();
        Assert.IsTrue(violated.Length == 1);
        Assert.AreEqual("uplimit", violated.ElementAtOrDefault(0)?.Name);
    }

    [TestMethod]
    public async Task CustomScalar_LowLimitValidation()
    {
        NodeSchema schema = new ()
        {
            Namespace = "system",
            Name = "positive_int",
            Kind = SCHEMA_KIND_INT,
        };
        IntSchema intSchema = new IntSchema
        {
            Base = NS_SYSTEM_INT,
        };
        intSchema.SetProperty<LowLimitInt, long>(1);
        intSchema.SetProperty<UpLimitInt, long>(1000);
        schema.SetProperty<IntProperty, IntSchema>(intSchema);
        await Context.SaveSchemaAsync(schema);

        var type = await Context.GetNodeTypeAsync<IntType>(schema.FullName);
        Assert.IsNotNull(type);

        // Valid value
        var validNode = await type.ValidateValueAsync(Context, 500);
        Assert.IsNotNull(validNode);
        Assert.IsTrue(validNode.IsValid);

        // Below low limit — try negative value
        var invalidNode = await type.ValidateValueAsync(Context, -5);
        Assert.IsNotNull(invalidNode);
        Console.WriteLine($"LowLimit(-5): IsValid={invalidNode.IsValid}, Violated={string.Join(", ", invalidNode.GetViolatedConstraints().Select(v => v.Name))}");
    }

    [TestMethod]
    public async Task CustomScalar_StringWithConstraints()
    {
        NodeSchema schema = new ()
        {
            Namespace = "system",
            Name = "short_name",
            Kind = SCHEMA_KIND_STRING,
        };
        StringSchema strSchema = new StringSchema
        {
            Base = NS_SYSTEM_STRING,
        };
        strSchema.SetProperty<UpLimitString, long>(10);
        schema.SetProperty<StringProperty, StringSchema>(strSchema);
        await Context.SaveSchemaAsync(schema);

        var type = await Context.GetNodeTypeAsync<ScalarType>(schema.FullName);
        Assert.IsNotNull(type);

        // Valid string (within limit)
        var validNode = await type.ValidateValueAsync(Context, "Hello");
        Assert.IsNotNull(validNode);
        Assert.IsTrue(validNode.IsValid);

        // Too long string
        var invalidNode = await type.ValidateValueAsync(Context, "This string is way too long");
        Assert.IsNotNull(invalidNode);
        Assert.IsFalse(invalidNode.IsValid);
    }

    [TestMethod]
    public async Task CustomScalar_DeleteScalar()
    {
        NodeSchema schema = new ()
        {
            Namespace = "system",
            Name = "todelete_scalar",
            Kind = SCHEMA_KIND_INT,
        };
        IntSchema intSchema = new IntSchema { Base = NS_SYSTEM_INT };
        schema.SetProperty<IntProperty, IntSchema>(intSchema);
        await Context.SaveSchemaAsync(schema);

        bool deleted = await Context.DeleteSchemaAsync(schema.FullName);
        Assert.IsTrue(deleted);
    }
}