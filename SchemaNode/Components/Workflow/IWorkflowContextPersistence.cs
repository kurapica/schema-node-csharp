using SchemaNode.Enum;

namespace SchemaNode.Components.Context;

/// <summary>
/// The interface for workflow context persistence.
/// </summary>
public interface IWorkflowContextPersistence
{
    /// <summary>
    /// Save the workflow context snapshot.
    /// </summary>
    Task SaveAsync(WorkflowContextSnapshot snapshot); 
    
    /// <summary>
    /// Remove the workflow context snapshot.
    /// </summary>
    Task RemoveAsync(WorkflowContextSnapshot snapshot);
    
    /// <summary>
    /// Gets an async enumerator for workflow context snapshots.
    /// </summary>
    Task<(IEnumerable<WorkflowContextSnapshot>, int)> ListAsync(string app, 
        string workflow, 
        Guid? rootId = null, 
        WorkflowStatus? status = null,
        int? skip = null,
        int? take = null);
    
}