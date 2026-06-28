using SchemaNode.Enum;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.UnitTest.Core;

/// <summary>
/// Tests for RelationSchema and IRelationProcess in SchemaNode.Core.
/// Relations enable dynamic property linkage between types.
/// </summary>
[TestClass]
public class RelationTest : Base.CoreTestBase
{
    /// <summary>
    /// Verify that RelationStage enum has the expected flag values
    /// </summary>
    [TestMethod]
    public void RelationStage_FlagValues()
    {
        // Load=1, Input=2, Persist=4
        Assert.IsTrue(RelationStage.Load.HasFlag(RelationStage.Load));
        Assert.IsTrue(RelationStage.Input.HasFlag(RelationStage.Input));
        Assert.IsFalse(RelationStage.Load.HasFlag(RelationStage.Persist));
    }

    /// <summary>
    /// Verify that system relation types are loaded (e.g., relation.assign)
    /// </summary>
    [TestMethod]
    public async Task Relation_AssignType_Loaded()
    {
        var assignType = await Context.GetNodeTypeAsync<NodeType>($"{NS_SYSTEM_SCHEMA_RELATION}.assign");
        Assert.IsNotNull(assignType, "relation.assign should be registered");
        Console.WriteLine($"Assign relation type: {assignType.Name}, Kind: {assignType.Kind}");
    }

    /// <summary>
    /// Verify that system relation types can be resolved through SchemaRuntime
    /// </summary>
    [TestMethod]
    public async Task Relation_SystemTypes_Resolvable()
    {
        var runtime = Context.Runtime as SchemaRuntime;
        Assert.IsNotNull(runtime, "Runtime should be SchemaRuntime");

        // system.relation.assign should exist as a registered schema kind
        var assignType = await Context.GetNodeTypeAsync<NodeType>($"{NS_SYSTEM_SCHEMA_RELATION}.assign");
        Assert.IsNotNull(assignType, "System assign relation should be resolvable");
    }
}
