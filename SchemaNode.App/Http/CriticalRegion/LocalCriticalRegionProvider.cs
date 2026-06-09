using System.Collections.Concurrent;

namespace SchemaNode.Components;

/// <summary>
/// Represents a local <see cref="CriticalRegion" /> provider.
/// Idle semaphores are purged after <see cref="IdleTtl"/> to prevent unbounded memory growth.
/// </summary>
public class LocalCriticalRegionProvider : ICriticalRegionProvider
{
    /// <summary>
    /// How long an idle (released, no waiters) semaphore entry stays in the cache before eviction.
    /// </summary>
    static readonly TimeSpan IdleTtl = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Minimum interval between two consecutive purge sweeps.
    /// </summary>
    static readonly TimeSpan PurgeInterval = TimeSpan.FromMinutes(2);

    #region Inner types

    sealed class SemaphoreEntry(SemaphoreSlim semaphore)
    {
        public readonly SemaphoreSlim Semaphore = semaphore;

        /// <summary>
        /// Ticks of the last release. 0 means the semaphore is currently held.
        /// </summary>
        public long LastReleaseTicks = Environment.TickCount64;
    }

    sealed class CriticalRegion(SemaphoreEntry entry) : ICriticalRegion
    {
        public void Dispose()
        {
            Interlocked.Exchange(ref entry.LastReleaseTicks, Environment.TickCount64);
            entry.Semaphore.Release();
        }
    }

    #endregion

    /// <inheritdoc />
    public ICriticalRegion Acquire(string name, TimeSpan? timeout = null)
    {
        SemaphoreEntry entry = Entries.GetOrAdd(name, _ => new SemaphoreEntry(new SemaphoreSlim(1, 1)));
        Interlocked.Exchange(ref entry.LastReleaseTicks, 0);

        if (timeout != null)
        {
            if (!entry.Semaphore.Wait(timeout.Value))
            {
                Interlocked.Exchange(ref entry.LastReleaseTicks, Environment.TickCount64);
                throw new TimeoutException();
            }
        }
        else
        {
            entry.Semaphore.Wait();
        }

        TryPurge();
        return new CriticalRegion(entry);
    }

    /// <inheritdoc />
    public async Task<ICriticalRegion> AcquireAsync(string name, TimeSpan? timeout = null)
    {
        SemaphoreEntry entry = Entries.GetOrAdd(name, _ => new SemaphoreEntry(new SemaphoreSlim(1, 1)));
        Interlocked.Exchange(ref entry.LastReleaseTicks, 0);

        if (timeout != null)
        {
            if (!await entry.Semaphore.WaitAsync(timeout.Value))
            {
                Interlocked.Exchange(ref entry.LastReleaseTicks, Environment.TickCount64);
                throw new TimeoutException();
            }
        }
        else
        {
            await entry.Semaphore.WaitAsync();
        }

        TryPurge();
        return new CriticalRegion(entry);
    }

    #region Purge

    static readonly ConcurrentDictionary<string, SemaphoreEntry> Entries = new();
    static long _lastPurgeTicks;

    /// <summary>
    /// Best-effort purge of idle semaphore entries.
    /// Only one thread performs the sweep; others skip if a sweep is already in progress or was done recently.
    /// </summary>
    static void TryPurge()
    {
        long now = Environment.TickCount64;
        long last = Volatile.Read(ref _lastPurgeTicks);
        if (now - last < PurgeInterval.TotalMilliseconds) return;
        if (Interlocked.CompareExchange(ref _lastPurgeTicks, now, last) != last) return;

        long ttlMs = (long)IdleTtl.TotalMilliseconds;
        foreach ((string key, SemaphoreEntry entry) in Entries)
        {
            long released = Volatile.Read(ref entry.LastReleaseTicks);
            if (released == 0) continue; // currently held
            if (now - released < ttlMs) continue; // not yet expired
            if (entry.Semaphore.CurrentCount == 0) continue; // someone acquired it between checks

            if (Entries.TryRemove(key, out SemaphoreEntry? removed))
            {
                removed.Semaphore.Dispose();
            }
        }
    }

    #endregion
}