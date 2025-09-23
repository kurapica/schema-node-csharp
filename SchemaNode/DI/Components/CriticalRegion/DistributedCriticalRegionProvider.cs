using Medallion.Threading;
using Medallion.Threading.Redis;
using StackExchange.Redis;

namespace SchemaNode.DI;

/// <summary>
/// Represents a distributed <see cref="CriticalRegion" /> provider.
/// </summary>
public class DistributedCriticalRegionProvider : ICriticalRegionProvider, IDisposable
{
    #region Inner type

    class CriticalRegion : ICriticalRegion
    {
        public CriticalRegion(IDistributedSynchronizationHandle handle)
        {
            _handle = handle;
        }

        IDistributedSynchronizationHandle _handle;

        public void Dispose()
        {
            _handle.Dispose();
        }
    }

    #endregion

    #region Constructors

    /// <summary>
    /// The distributed critical region provider constructor
    /// </summary>
    public DistributedCriticalRegionProvider(ConfigurationOptions options)
    {
        _redisConnection = ConnectionMultiplexer.Connect(options);
        _distributedLockProvider = new RedisDistributedSynchronizationProvider(_redisConnection.GetDatabase());
    }

    #endregion

    #region Acquire

    /// <inheritdoc />
    public ICriticalRegion Acquire(string name, TimeSpan? timeout = null)
    {
        IDistributedLock distributedLock = _distributedLockProvider.CreateLock(GetLockKey(name));
        IDistributedSynchronizationHandle distributedSynchronizationHandle = distributedLock.Acquire(timeout);
        return new CriticalRegion(distributedSynchronizationHandle);
    }

    /// <inheritdoc />
    public async Task<ICriticalRegion> AcquireAsync(string name, TimeSpan? timeout = null)
    {
        IDistributedLock distributedLock = _distributedLockProvider.CreateLock(GetLockKey(name));
        IDistributedSynchronizationHandle distributedSynchronizationHandle = await distributedLock.AcquireAsync(timeout);
        return new CriticalRegion(distributedSynchronizationHandle);
    }

    #endregion

    #region Implementations

    string GetLockKey(string name) => $"DistributedCritcalRegion_{name}";

    public void Dispose()
    {
        _redisConnection.Dispose();
    }

    readonly ConnectionMultiplexer _redisConnection;
    readonly IDistributedLockProvider _distributedLockProvider;

    #endregion
}