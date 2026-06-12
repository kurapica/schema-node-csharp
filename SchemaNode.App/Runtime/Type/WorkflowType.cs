using System.Reflection;
using SchemaNode.Attribute;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Schema;
using SchemaNode.Utility;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory workflow schema representation
/// </summary>
public sealed class WorkflowType: NodeType
{
    #region Data

    /// <summary>
    /// The workflow type
    /// </summary>
    public string Kind { get; set; }
    
    /// <summary>
    /// The workflow payload type
    /// </summary>
    public string? Payload { get; set; }
        
    /// <summary>
    /// The state schema type for constructor
    /// </summary>
    public string? State { get; set; }
    
    /// <summary>
    /// The session schema type
    /// </summary>
    public string? Session { get; set; }
    
    /// <summary>
    /// The workflow arguments fetch from workflow context
    /// </summary>
    public FuncArg[]? Args { get; set; } = [];
        
    #endregion
    
    #region Status
    
    /// <inheritdoc />
    public override SchemaType Type => SchemaType.Workflow;
    
    #endregion
    
    #region Method

    /// <inheritdoc />
    public override Task LoadAsync(SchemaContext context)
    {
        WorkflowSchema? workflow = schema.Workflow;
        
        // Data
        WorkflowMode = workflow?.Mode ?? WorkflowMode.Workflow;
        Payload = workflow?.Payload;
        State = workflow?.State;
        Session = workflow?.Session;
        Args = workflow?.Args;

        if (workflow == null) Status = SchemaNodeStatus.NoDefinition;

        return Task.CompletedTask;
    }

    #endregion
}