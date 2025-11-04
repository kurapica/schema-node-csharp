using System.Text.Json.Nodes;
using SchemaNode.Attribute;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Components;

[SchemaType($"{NS_SYSTEM_WORKFLOW}.call")]
public class CallWorkflow: FunctionWorkflow
{
    /// <inheritdoc />
    public override Task<JsonNode?> ExecuteAsync(string func, JsonArray args)
    {
        return Context!.CallFunctionAsync(func, args);
    }
}