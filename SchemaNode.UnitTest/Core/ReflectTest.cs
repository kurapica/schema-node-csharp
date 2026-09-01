using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.UnitTest.Core;

[TestClass]
public class ReflectTest : Base.CoreTestBase
{
    [TestMethod]
    public async Task SystemMath_Add()
    {
        var usageType = await Context.GetNodeTypeAsync<FunctionType>(
                $"{NS_SYSTEM_SCHEMA_REFLECT_TYPE}.{nameof(SchemaNode.Function.Reflect.Type.getusagetype)}");
        Assert.IsNotNull(usageType);
        
        var type = await usageType.CallAsync<string>(Context, [NS_SYSTEM_INT]);
        Assert.IsNotNull(type);
        Assert.AreEqual($"{NS_SYSTEM_SCHEMA_INT}.usage", type);
    }
}