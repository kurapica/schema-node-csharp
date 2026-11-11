using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Property.Core;
using SchemaNode.Property.String;
using SchemaNode.Runtime;
using System.Text.Json.Nodes;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.UnitTest.Core;

[TestClass]
public class TempRelationDiagnosticTest : Base.CoreTestBase
{
    [TestMethod]
    public async Task Diagnose_RelationSchema()
    {
        Context.FullMode = true;
        var type = await Context.GetNodeTypeAsync<StructType>($"{NS_SYSTEM_SCHEMA_RELATION}.schema");
        Assert.IsNotNull(type);

        Console.WriteLine($"== type {type.Name} error={type.Error}");
        foreach (var r in type.GetRelations())
            Console.WriteLine($"REL target={r.Target} kind={r.Kind} prop={r.Property?.Name} csharp={r.Property?.GetCsharpType()} error={r.Error} process={r.Process?.GetType().Name}");

        foreach (var f in type.GetFields())
        {
            Console.WriteLine($"FIELD {f.Name} type={f.Type?.Name} props=[{string.Join(",", f.GetProperties<IProperty>().Select(p => p.GetType().Name))}]");
        }

        var node = type.Create(Context, new JsonObject
        {
            ["target"] = "min",
            ["kind"] = "assign"
        }) as StructNode;
        Assert.IsNotNull(node);

        await Task.Delay(300);

        var propNode = node!.GetAccessValue("property") as DataNode;
        var targetTypeNode = node.GetAccessValue("targetType") as DataNode;
        var targetKindNode = node.GetAccessValue("targetKind") as DataNode;
        var ownerKindNode = node.GetAccessValue("ownerKind") as DataNode;

        Console.WriteLine($"property null={propNode is null} props=[{string.Join(",", propNode?.GetProperties<IProperty>().Select(p => p.GetType().Name) ?? [])}]");
        Console.WriteLine($"property entrySource={propNode?.GetProperty<EntrySource>()?.Value?.Func ?? "<null>"}");
        Console.WriteLine($"targetType value={targetTypeNode?.GetValue<object>() ?? "<null>"} props=[{string.Join(",", targetTypeNode?.GetProperties<IProperty>().Select(p => p.GetType().Name) ?? [])}]");
        Console.WriteLine($"targetKind value={targetKindNode?.GetValue<object>() ?? "<null>"}");
        Console.WriteLine($"ownerKind value={ownerKindNode?.GetValue<object>() ?? "<null>"}");

        Assert.IsTrue(propNode?.GetProperty<EntrySource>() is not null, "the property field must carry an EntrySource relation result");
    }
}
