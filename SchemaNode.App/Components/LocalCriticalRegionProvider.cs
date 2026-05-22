using System.Collections.Concurrent;

namespace SchemaNode.App.Components;

/// <summary>
/// In-process critical-region provider using <see cref="SemaphoreSlim"/> per named lock.
/// </summary>
public sealed class LocalCriticalRegionProvider : ICriticalRegionProvider
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);

    private SemaphoreSlim GetSemaphore(string name)
        => _locks.GetOrAdd(name, _ => new SemaphoreSlim(1, 1));

    /// <inheritdoc/>
    public ICriticalRegion Acquire(string name, TimeSpan? timeout = null)
    {
        var sem = GetSemaphore(name);
        if (timeout.HasValue)
            sem.Wait(timeout.Value);
        else
            sem.Wait();
        return new CriticalRegion(sem);
    }

    /// <inheritdoc/>
    public async Task<ICriticalRegion> AcquireAsync(string name, TimeSpan? timeout = null)
    {
        var sem = GetSemaphore(name);
        if (timeout.HasValue)
            await sem.WaitAsync(timeout.Value);
        else
            await sem.WaitAsync();
        return new CriticalRegion(sem);
    }

    private sealed class CriticalRegion(SemaphoreSlim semaphore) : ICriticalRegion
    {
        public void Dispose() => semaphore.Release();
    }
}
