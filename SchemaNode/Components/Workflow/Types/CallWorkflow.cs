using System.Text.Json.Nodes;
using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Components;

[SchemaType($"{NS_SYSTEM_WORKFLOW}.call")]
public class CallWorkflow: FunctionWorkflow, IWorkflowPayload
{
    /// <summary>
    /// Process the func call
    /// </summary>
    public async Task ProcessAsync(WorkflowContext context)
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
            
            JsonNode? result = await context.CallFunctionAsync(Function, args, PayloadType != null ? [PayloadType.Name] : null);
            SetPayload(context, result);
        }
        catch (Exception e)
        {
            context.Error(this, e.GetInnermostException().Message);
        }
    }
}