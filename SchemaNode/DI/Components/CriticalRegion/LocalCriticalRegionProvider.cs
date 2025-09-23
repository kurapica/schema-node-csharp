using System.Collections.Concurrent;

namespace SchemaNode.DI;

/// <summary>
/// Represents a local <see cref="CriticalRegion" /> provider.
/// </summary>
public class LocalCriticalRegionProvider : ICriticalRegionProvider
{
    #region Inner type

    class CriticalRegion: ICriticalRegion
    {
        public CriticalRegion(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        SemaphoreSlim _semaphore;

        public void Dispose()
        {
            _semaphore.Release();
        }
    }

    #endregion

    /// <inheritdoc />
    public ICriticalRegion Acquire(string name, TimeSpan? timeout = null)
    {
        SemaphoreSlim semaphore = Semaphores.GetOrAdd(name, _ => new SemaphoreSlim(1, 1));
        if (timeout != null)
        {
            if (!semaphore.Wait(timeout.Value))
            {
                throw new TimeoutException();
            }
        }
        else
        {
            semaphore.Wait();
        }
        return new CriticalRegion(semaphore);
    }

    /// <inheritdoc />
    public async Task<ICriticalRegion> AcquireAsync(string name, TimeSpan? timeout = null)
    {
        SemaphoreSlim semaphore = Semaphores.GetOrAdd(name, _ => new SemaphoreSlim(1, 1));
        if (timeout != null)
        {
            if (!await semaphore.WaitAsync(timeout.Value))
            {
                throw new TimeoutException();
            }
        }
        else
        {
            await semaphore.WaitAsync();
        }
        return new CriticalRegion(semaphore);
    }

    static readonly ConcurrentDictionary<string, SemaphoreSlim> Semaphores = new();
}