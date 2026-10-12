using SchemaNode.Context;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.UnitTest.Core;

[TestClass]
public class ReflectTest : Base.CoreTestBase
{
    [TestMethod]
    public async Task GetUsageSchema()
    {
        var usageType = await Context.GetNodeTypeAsync<FunctionType>(
                $"{NS_SYSTEM_SCHEMA_REFLECT_TYPE}.{nameof(SchemaNode.Function.Reflect.Type.getusagetype)}");
        Assert.IsNotNull(usageType);
        
        var type = await usageType.CallAsync<string>(Context, [NS_SYSTEM_INT]);
        Assert.IsNotNull(type);
        Assert.AreEqual($"{NS_SYSTEM_SCHEMA_INT}.usage", type);
    }
    
    [TestMethod]
    public async Task GetNodeSchemaKinds()
    {
        var kindType = await Context.GetNodeTypeAsync<EnumType>(
            $"{NS_SYSTEM_SCHEMA_NODE}.kind");
        Assert.IsNotNull(kindType);

        var access = await kindType.GetEnumEntryAccessAsync(Context, "");
        Assert.IsNotNull(access);
        Assert.AreEqual(1, access.Length);
        Assert.IsGreaterThan(5, access[0].Children?.Length ?? 0);
    }
}