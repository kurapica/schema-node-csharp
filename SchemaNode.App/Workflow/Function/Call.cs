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
using SchemaNode.Function;
using SchemaNode.Property.Common;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;
using SchemaType = SchemaNode.Property.Core.SchemaType;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Workflow;

[Meta<WorkflowKind>(WORKFLOW_KIND_CALL)]
[Meta<OfSchema>(SCHEMA_KIND_WORKFLOW)]
[Meta<SchemaType>($"{NS_SYSTEM_WORKFLOW}.call")]
public class Call: BaseWorkflow, 
    IWorkflowPayload<Object> // all value type can be used as object type
{
    private FunctionType? _function;
    private CallArg[]? _args;

    public override async Task LoadAsync(SchemaContext context, AppWorkflowNodeSchema schema)
    {
        FuncCall? call = schema.GetProperty<CallProperty>()?.Value;
        if (call == null)
        {
            schema.Error ??= AppErrorCodes.WORKFLOW_CALL_FUNC_NOT_VALID;
            return;
        }
        _function = await context.GetNodeTypeAsync<FunctionType>(call.Func);
        if (_function == null)
        {
            schema.Error ??= AppErrorCodes.WORKFLOW_CALL_FUNC_NOT_VALID;
            return;
        }
        _args = call.Args;
    }

    /// <summary>
    /// Process the func call
    /// </summary>
    public async Task ProcessAsync(WorkflowContext context)
    {
        if (_function is null)
        {
            context.Error(this, "The function is not defined.");    
            return;
        }
    
        try
        {
            SetPayload(context, await _function.CallAsync<DataNode>(context,
                _args?.Select<CallArg, object?>(callArg => string.IsNullOrEmpty(callArg.Source)
                    ? callArg.Value?.DeepClone()
                    : context.GetWorkflowPayload(callArg.Source)).ToArray() ?? [], PayloadType?.Name));
        }
        catch (Exception e)
        {
            context.Error(this, e.GetInnermostException().Message);
        }
    }
}

[Meta<ForSchema>(SCHEMA_KIND_APP_WORKFLOW_NODE)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY}.workflow.call")]
[Relation<Visible>($"{NS_SYSTEM_SCHEMA_REFLECT}.workflow.{nameof(SystemAppReflect.Workflow.iskind)}", $"${nameof(AppWorkflowNodeSchema.Type)}", WORKFLOW_KIND_CALL)]
[RelationAssign<Valid>($"{nameof(Call)}.{nameof(FuncCall.Func)}", NS_SYSTEM_SCHEMA_REFLECT_FUNC_WITH_RETURN, NODE_SELF, $"${nameof(AppWorkflowNodeSchema.Type)}")]
public class CallProperty : Property<FuncCall>;