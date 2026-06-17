using SchemaNode.Attribute;
using SchemaNode.Property;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Core;
using SchemaNode.Property.Record;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;
using ValueType = SchemaNode.Schema.ValueType;

namespace SchemaNode.Workflow;

[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP_WORKFLOW}.call")]
public class FunctionWorkflowSchema
{
    /// <summary>
    /// The return value
    /// </summary>
    [Meta<SchemaType>(typeof(ValueType))]
    public string Return { get; set; }  = string.Empty;
    
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

[Meta<Alias>(WORKFLOW_KIND_CALL_FUNC)]
[Meta<ForSchema>(SCHEMA_KIND_APP_WORKFLOW)]
[Meta<WorkflowKind>(WORKFLOW_KIND_CALL_FUNC)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY}.workflow.call")]
public class FunctionWorkflowProperty : Property<FunctionWorkflowSchema>;

/// <summary>
/// The workflow class associated with function call
/// </summary>
public abstract class FuncCallWorkflow: Workflow
{
    /// <summary>
    /// The function type
    /// </summary>
    public FunctionType? Function { get; set; }
    
    /// <summary>
    /// The function call arguments
    /// </summary>
    public CallArg[]? FuncArgs { get; set; }
}