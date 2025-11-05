using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Components;
using SchemaNode.Runtime;

namespace SchemaNode.Context;

/// <summary>
/// The workflow context
/// </summary>
public class WorkflowContext: SchemaContext, IDisposable
{
    #region Constructors

    public WorkflowContext(IServiceScopeFactory scopeFactory): this(scopeFactory.CreateScope())
    {
    }
    
    private WorkflowContext(IServiceScope scope): base(scope.ServiceProvider)
    {
        _scope = scope;
    }
    
    
    #endregion
    
    #region Properties

    /// <summary>
    /// The workflow unique identifier
    /// </summary>
    public Guid WorkflowId { get; set; } = Guid.CreateVersion7();
    
    #endregion
    
    #region Methods
    
    /// <summary>
    /// Init the workflow context with workflow
    /// </summary>
    public void Initialize(AppWorkflowType workflow)
    {
        
    }
    
    /// <summary>
    /// The workflow node is done
    /// </summary>
    public void Done(Workflow workflow)
    {
    }

    /// <summary>
    /// The workflow node has error
    /// </summary>
    public void Error(Workflow workflow, Exception exception)
    {
    }
    
    #endregion

    #region IDisposable
    
    public void Dispose() => _scope.Dispose();
    private readonly IServiceScope _scope;

    #endregion
}