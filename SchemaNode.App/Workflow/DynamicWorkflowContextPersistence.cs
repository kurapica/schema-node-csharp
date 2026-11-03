using SchemaNode.Context;
using SchemaNode.Data;
using SchemaNode.Enum;

namespace SchemaNode.Workflow;

public class DynamicWorkflowContextPersistence(SchemaContext context): IWorkflowContextPersistence
{
    /// <inheritdoc />
    public async Task SaveAsync(WorkflowContextSnapshot snapshot)
    {
        try
        {
            await context.BeginTransactionAsync().ConfigureAwait(false);
            await context.SaveEntityAsync(snapshot).ConfigureAwait(false);

            if (snapshot.Forks is { Length: > 0 })
                await SaveForksAsync(snapshot.Forks).ConfigureAwait(false);
            
            await context.CommitTransactionAsync(true).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            context.LogError(e, "Failed to save workflow context snapshot {SnapshotId} for workflow {BaseWorkflow} in app {App}",
                snapshot.Id, snapshot.Workflow, snapshot.App);
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(WorkflowContextSnapshot snapshot)
    {
        try
        {
            await context.BeginTransactionAsync().ConfigureAwait(false);
            await context.DeleteEntityAsync(snapshot).ConfigureAwait(false);
            if (snapshot.Forks is { Length: > 0 })
                await RemoveForksAsync(snapshot.Forks).ConfigureAwait(false);
            await context.CommitTransactionAsync().ConfigureAwait(false);
        }
        catch (Exception e)
        {
            context.LogError(e, "Failed to remove workflow context snapshot {SnapshotId} for workflow {BaseWorkflow} in app {App}",
                snapshot.Id, snapshot.Workflow, snapshot.App);
        }
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<WorkflowContextSnapshot>, int)> ListAsync(string app, string workflow, Guid? rootId = null, WorkflowStatus? status = null,
        int? skip = null, int? take = null)
    {
        try
        {
            Guid root = rootId ?? Guid.Empty;
            WorkflowStatus chkStatus = status ?? WorkflowStatus.Running;
            
            if (status == WorkflowStatus.Running)
            {
                var list = await context.GetEntitiesAsync<WorkflowContextSnapshot>(s =>
                    s.Workflow == workflow && s.RootId == root && s.Status == chkStatus).ConfigureAwait(false);
                foreach (var snapshot in list)
                {
                    var forks = await ListAsync(app, workflow, snapshot.Id, status).ConfigureAwait(false);
                    snapshot.Forks = forks.Item1.ToArray();
                }
                return (list, list.Count);
            }
            else
            {
                return await context.GetEntitiesWithTotalAsync<WorkflowContextSnapshot>(s =>
                    s.Workflow == workflow && s.RootId == root && s.Status == chkStatus, take ?? 50, skip ?? 0).ConfigureAwait(false);
            }
        }
        catch (Exception e)
        {
            context.LogError(e, "Failed to list workflow context snapshots for workflow {BaseWorkflow} in app {App}",
                workflow, app);
            throw;
        }
    }

    #region Utility Methods

    async Task SaveForksAsync(WorkflowContextSnapshot[] forks)
    {
        foreach (var fork in forks)
        {
            await SaveAsync(fork).ConfigureAwait(false);
            if (fork.Forks is { Length: > 0 })
                await SaveForksAsync(fork.Forks).ConfigureAwait(false);
        }
    }
    
    async Task RemoveForksAsync(WorkflowContextSnapshot[] forks)
    {
        foreach (var fork in forks)
        {
            await RemoveAsync(fork).ConfigureAwait(false);
            if (fork.Forks is { Length: > 0 })
                await RemoveForksAsync(fork.Forks).ConfigureAwait(false);
        }
    }
    #endregion
}