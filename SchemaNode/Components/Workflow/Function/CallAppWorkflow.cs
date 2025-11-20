using System.Text.Json.Nodes;
using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Components;

[Schema($"{NS_SYSTEM_WORKFLOW}.func.appcall")]
public class CallAppWorkflow: FunctionWorkflow, 
    IWorkflowState<CallWorkflowState>,
    IWorkflowPayload
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
        
        for (int attempt = (State?.Retry ?? 0) + 1; attempt > 0; attempt--)
        {
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

                JsonNode? result = await context.CallFunctionAsync(Function, args,
                    PayloadType != null ? [PayloadType.Name] : null, target);
                if (State?.Result ?? false)
                {
                    if (result == null || result.IsEmpty())
                    {
                        if (attempt > 1)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(State?.Delay ?? 1));
                            continue;
                        }
                        else
                        {
                            context.Error(this, "The function call result is empty.");
                            return;
                        }
                    }
                }

                SetPayload(context, result);
                break;
            }
            catch (Exception e)
            {
                if (attempt != 1) continue;
                context.Error(this, e.GetInnermostException().Message);
                return;
            }
        }
    }

    public CallWorkflowState? State { get; set; }
}
