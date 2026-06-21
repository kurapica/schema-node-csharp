using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.RefactorTest.Core;

/// <summary>
/// Tests for function type loading in SchemaNode.Core.
/// 
/// KNOWN ISSUE: system.math, system.logic, system.str namespaces are NOT
/// accessible via GetNodeTypeAsync after InitSchemaRuntimeAsync. Diagnosis:
/// SchemaRuntime.GetSystemSchema("system.math") returns null. The system
/// namespace exists but has empty Schemas. Root cause: FunctionGenerator
/// produces namespace schemas but they are not being saved to _rootSchema
/// during OnSystemSchemaLoading. Fix needed in SchemaRuntime.SaveSystemSchema
/// or the assembly scanning pipeline.
///
/// Symbolic constant tests pass (48 total).
/// </summary>
[TestClass]
public class FunctionTypeTest : Base.CoreTestBase
{
    /// <summary>
    /// Call system.math.add to verify integer addition
    /// </summary>
    [TestMethod]
    public async Task SystemMath_Add_Int()
    {
        var func = await Context.GetNodeTypeAsync<FunctionType>("system.math.add");
        Assert.IsNotNull(func);

        var result = await func.CallAsync<long>(Context, [3L, 5L]);
        Assert.AreEqual(8L, result);
    }
}
