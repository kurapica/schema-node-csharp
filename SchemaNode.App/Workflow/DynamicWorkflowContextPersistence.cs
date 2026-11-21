using SchemaNode.Context;
using SchemaNode.Data;
using SchemaNode.Data.Entity;
using SchemaNode.Enum.Workflow;

namespace SchemaNode.Workflow;

public class DynamicWorkflowContextPersistence(SchemaContext context): IWorkflowContextPersistence
{
    /// <inheritdoc />
    public async Task SaveAsync(WorkflowContextSnapshot snapshot)
    {
        try
        {
            await context.BeginTransactionAsync().ConfigureAwait(false);
            await context.SaveEntityAsync((AppWorkflowContextSnapshotEntity)snapshot).ConfigureAwait(false);

            if (snapshot.Forks is { Length: > 0 })
                await SaveForksAsync(snapshot.Forks).ConfigureAwait(false);
            
            await context.CommitTransactionAsync(true).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            context.LogError(e, "Failed to save workflow context snapshot {SnapshotId} for workflow {Workflow}",
                snapshot.Id, snapshot.Workflow);
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(WorkflowContextSnapshot snapshot)
    {
        try
        {
            await context.BeginTransactionAsync().ConfigureAwait(false);
            await context.DeleteEntityAsync((AppWorkflowContextSnapshotEntity)snapshot).ConfigureAwait(false);
            if (snapshot.Forks is { Length: > 0 })
                await RemoveForksAsync(snapshot.Forks).ConfigureAwait(false);
            await context.CommitTransactionAsync().ConfigureAwait(false);
        }
        catch (Exception e)
        {
            context.LogError(e, "Failed to remove workflow context snapshot {SnapshotId} for workflow {Workflow}",
                snapshot.Id, snapshot.Workflow);
        }
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<WorkflowContextSnapshot>, int)> ListAsync(string workflow, Guid? rootId = null, WorkflowStatus? status = null,
        int? skip = null, int? take = null)
    {
        try
        {
            Guid root = rootId ?? Guid.Empty;
            WorkflowStatus chkStatus = status ?? WorkflowStatus.Running;
            
            if (status == WorkflowStatus.Running)
            {
                var entities = await context.GetEntitiesAsync<AppWorkflowContextSnapshotEntity>(s =>
                    s.Workflow == workflow && s.RootId == root && s.Status == chkStatus).ConfigureAwait(false);

                List<WorkflowContextSnapshot> list = [];
                foreach (var entity in entities)
                {
                    WorkflowContextSnapshot snapshot = (WorkflowContextSnapshot)entity;
                    var forks = await ListAsync(workflow, snapshot.Id, status).ConfigureAwait(false);
                    snapshot.Forks = forks.Item1.ToArray();
                    list.Add(snapshot);
                }
                return (list, list.Count);
            }
            else
            {
                var (entities, total) = await context.GetEntitiesWithTotalAsync<AppWorkflowContextSnapshotEntity>(s =>
                    s.Workflow == workflow && s.RootId == root && s.Status == chkStatus, take ?? 50, skip ?? 0).ConfigureAwait(false);
                return (entities.Select(e => (WorkflowContextSnapshot)e), total);
            }
        }
        catch (Exception e)
        {
            context.LogError(e, "Failed to list workflow context snapshots for workflow {Workflow}", workflow);
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
