using SchemaNode.Runtime;
using SchemaNode.Schema;

namespace SchemaNode.Components;

/// <summary>
/// The function workflow class
/// </summary>
public abstract class FunctionWorkflow: Workflow
{
    /// <summary>
    /// The given function
    /// </summary>
    public FunctionType Function { get; set; } = null!;
    
    /// <summary>
    /// The call function arguments
    /// </summary>
    public FuncCallArg[] Args { get; set; } = [];
}