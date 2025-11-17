using SchemaNode.Runtime;
using SchemaNode.Schema;

namespace SchemaNode.Components;

/// <summary>
/// The workflow class associated with function call
/// </summary>
public abstract class FunctionWorkflow: Workflow
{
    /// <summary>
    /// The function type
    /// </summary>
    public FunctionType? Function { get; set; }
    
    /// <summary>
    /// The function call arguments
    /// </summary>
    public FuncCallArg[]? FuncArgs { get; set; }
}