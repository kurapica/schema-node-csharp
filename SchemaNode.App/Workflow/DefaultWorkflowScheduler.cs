using System.Threading.Channels;
using SchemaNode.Context;
using SchemaNode.Utility;

namespace SchemaNode.Workflow;

/// <summary>
/// The default workflow scheduler
/// </summary>
public sealed class DefaultWorkflowScheduler : IWorkflowScheduler, IDisposable
{
    private readonly Channel<WorkflowContext> _channel;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task[] _workers;

    /// <summary>
    /// Init the workflow scheduler with the specified max workers
    /// </summary>
    public DefaultWorkflowScheduler(int maxWorkers = 0)
    {
        // keep low cpu usage by default
        if (maxWorkers <= 0) maxWorkers = Math.Max(1, Environment.ProcessorCount / 4);
        var options = new UnboundedChannelOptions { SingleReader = false, SingleWriter = false };
        _channel = Channel.CreateUnbounded<WorkflowContext>(options);
        
        _workers = new Task[maxWorkers];
        for (int i = 0; i < maxWorkers; i++)
        {
            _workers[i] = Task.Run(() => WorkerLoopAsync(_cts.Token));
        }
    }

    // Schedule a new workflow context
    public void Schedule(WorkflowContext context)
    {
        _cts.Token.ThrowIfCancellationRequested();
        _channel.Writer.TryWrite(context);
    }

    // Fetch and process workflow contexts
    private async Task WorkerLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (WorkflowContext ctx in _channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                try
                {
                    await ctx.ProcessAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex.GetInnermostException().Message);
                }
            }
        }
        catch (OperationCanceledException) {}
        catch { /* ignore */ }
    }

    // Release
    public void Dispose()
    {
        _channel.Writer.Complete();
        _cts.Cancel();
        try
        {
            Task.WaitAll(_workers);
        }
        catch { /* Skip */ }
        _cts.Dispose();
    }
}
