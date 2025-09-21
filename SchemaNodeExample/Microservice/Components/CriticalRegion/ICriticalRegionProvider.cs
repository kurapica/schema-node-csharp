namespace SchemaNode.Example;

/// <summary>
/// Defines the contract of a <see cref="CriticalRegion" /> provider.
/// </summary>
public interface ICriticalRegionProvider
{
    /// <summary>
    /// Acquires a critical region.
    /// </summary>
    ICriticalRegion Acquire(string name, TimeSpan? timeout = null);

    /// <summary>
    /// Acquires a critical region asynchronously.
    /// </summary>
    Task<ICriticalRegion> AcquireAsync(string name, TimeSpan? timeout = null);
}

public interface ICriticalRegion: IDisposable
{
}