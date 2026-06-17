using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Core;
using SchemaNode.Property.Record;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Text.Json.Nodes;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;
using SchemaType = SchemaNode.Property.Core.SchemaType;
using ValueType = SchemaNode.Schema.ValueType;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Workflow;

[Meta<WorkflowKind>(WORKFLOW_KIND_CALL)]
[Meta<OfSchema>(SCHEMA_KIND_WORKFLOW)]
[Meta<SchemaType>($"{NS_SYSTEM_WORKFLOW}.call")]
public class CallWorkflow: BaseWorkflow, 
    IWorkflowSettings<CallWorkflowState>,
    IWorkflowPayload
{
    /// <summary>
    /// The function type
    /// </summary>
    public FunctionType? Function { get; set; }

    /// <summary>
    /// The function call arguments
    /// </summary>
    public CallArg[]? FuncArgs { get; set; }
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
        
        for (int attempt = (Settings?.Retry ?? 0) + 1; attempt > 0; attempt--)
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
                        DataNode? payload = context.GetWorkflowPayload(callArg.Name);
                        args.Add(payload?.ToJson());
                    }
                }

                JsonNode? result = await context.CallFunctionAsync(Function, args, PayloadType?.Name);
                if (Settings?.Result ?? false)
                {
                    if (result == null || result.IsEmpty())
                    {
                        if (attempt > 1)
                        {
                            await Task.Delay(TimeSpan.FromMilliseconds(Settings?.Delay ?? 1));
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

    public CallWorkflowState? Settings { get; set; }
}


[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP_WORKFLOW}.call")]
public class CallWorkflowSchema
{
    /// <summary>
    /// The return value
    /// </summary>
    [Meta<SchemaType>(typeof(ValueType))]
    public string Return { get; set; } = string.Empty;

    /// <summary>
    /// The function name if type is Function
    /// </summary>
    [Meta<SchemaType>(typeof(FuncType))]
    [Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_FUNC_WITH_RETURN, NODE_SELF, $"${nameof(Return)}")]
    public string Func { get; set; } = string.Empty;

    /// <summary>
    /// The function call arguments
    /// </summary>
    public CallArg[] Args { get; set; } = [];
}

[Meta<Alias>(WORKFLOW_KIND_CALL)]
[Meta<ForSchema>(SCHEMA_KIND_APP_WORKFLOW)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY}.workflow.call")]
public class FunctionWorkflowProperty : Property<CallWorkflowSchema>;


/// <summary>
/// The call app workflow state
/// </summary>
[Schema($"{NS_SYSTEM_WORKFLOW}.func.callstate")]
public class CallWorkflowState
{
    /// <summary>
    /// Result required
    /// </summary>
    public bool? Result { get; set; }
    
    /// <summary>
    /// The retry count
    /// </summary>
    public int? Retry { get; set; }
    
    /// <summary>
    /// The delay milliseconds between retries
    /// </summary>
    public int? Delay { get; set; }
}