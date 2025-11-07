using SchemaNode.Runtime;
using SchemaNode.Schema;

namespace SchemaNode.Components;

/// <summary>
/// The function workflow class
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