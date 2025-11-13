using System.Text.Json.Nodes;
using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Components;

[SchemaType($"{NS_SYSTEM_WORKFLOW}.appcall")]
public class CallAppWorkflow: FunctionWorkflow, IWorkflowPayload
{
    /// <summary>
    /// Process the func call with app target
    /// </summary>
    public async Task ProcessAsync(WorkflowContext context, string target)
    {
        if (Function is null)
        {
            context.Error(this, "The function is not defined.");    
            return;
        }

        try
        {
            JsonArray args = [];
            foreach (FuncCallArg callArg in FuncArgs!)
            {
                if (string.IsNullOrEmpty(callArg.Name))
                {
                    args.Add(callArg.Value?.DeepClone());
                }
                else
                {
                    AnySchemaNode? payload = context.GetWorkflowPayload(callArg.Name);
                    args.Add(payload?.ToJson());
                }
            }
            
            JsonNode? result = await context.CallFunctionAsync(Function, args, PayloadType != null ? [PayloadType.Name] : null, target);
            SetPayload(context, result);
        }
        catch (Exception e)
        {
            context.Error(this, e.GetInnermostException().Message);
        }
    }
}