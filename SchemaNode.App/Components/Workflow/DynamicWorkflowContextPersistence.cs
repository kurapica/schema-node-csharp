using Microsoft.Extensions.Logging;
using SchemaNode.Context;
using SchemaNode.Enum;

namespace SchemaNode.Components.Context;

public class DynamicWorkflowContextPersistence(SchemaContext context): IWorkflowContextPersistence
{
    /// <inheritdoc />
    public async Task SaveAsync(WorkflowContextSnapshot snapshot)
    {
        try
        {
            await context.BeginTransactionAsync();
            await context.SaveEntityAsync(snapshot.App, snapshot);

            if (snapshot.Forks is { Length: > 0 })
                await SaveForksAsync(snapshot.Forks);
            
            await context.CommitTransactionAsync(true);
        }
        catch (Exception e)
        {
            context.Logger.LogError(e, "Failed to save workflow context snapshot {SnapshotId} for workflow {Workflow} in app {App}",
                snapshot.Id, snapshot.Workflow, snapshot.App);
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(WorkflowContextSnapshot snapshot)
    {
        try
        {
            await context.BeginTransactionAsync();
            await context.DeleteEntityAsync(snapshot.App, snapshot);
            if (snapshot.Forks is { Length: > 0 })
                await RemoveForksAsync(snapshot.Forks);
            await context.CommitTransactionAsync();
        }
        catch (Exception e)
        {
            context.Logger.LogError(e, "Failed to remove workflow context snapshot {SnapshotId} for workflow {Workflow} in app {App}",
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
                var list = await context.GetEntitiesAsync<WorkflowContextSnapshot>(app, s =>
                    s.Workflow == workflow && s.RootId == root && s.Status == chkStatus);
                foreach (var snapshot in list)
                {
                    var forks = await ListAsync(app, workflow, snapshot.Id, status);
                    snapshot.Forks = forks.Item1.ToArray();
                }
                return (list, list.Count);
            }
            else
            {
                return await context.GetEntitiesAsync<WorkflowContextSnapshot>(app, s =>
                    s.Workflow == workflow && s.RootId == root && s.Status == chkStatus, take ?? 50, skip ?? 0);
            }
        }
        catch (Exception e)
        {
            context.Logger.LogError(e, "Failed to list workflow context snapshots for workflow {Workflow} in app {App}",
                workflow, app);
            throw;
        }
    }

    #region Utility Methods

    async Task SaveForksAsync(WorkflowContextSnapshot[] forks)
    {
        foreach (var fork in forks)
        {
            await SaveAsync(fork);
            if (fork.Forks is { Length: > 0 })
                await SaveForksAsync(fork.Forks);
        }
    }
    
    async Task RemoveForksAsync(WorkflowContextSnapshot[] forks)
    {
        foreach (var fork in forks)
        {
            await RemoveAsync(fork);
            if (fork.Forks is { Length: > 0 })
                await RemoveForksAsync(fork.Forks);
        }
    }
    #endregion
}