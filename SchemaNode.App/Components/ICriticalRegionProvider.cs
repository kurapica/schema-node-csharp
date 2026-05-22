namespace SchemaNode.App.Components;

/// <summary>
/// Provides critical-region (distributed lock) acquisition.
/// </summary>
public interface ICriticalRegionProvider
{
    /// <summary>Synchronously acquires a named critical region.</summary>
    ICriticalRegion Acquire(string name, TimeSpan? timeout = null);

    /// <summary>Asynchronously acquires a named critical region.</summary>
    Task<ICriticalRegion> AcquireAsync(string name, TimeSpan? timeout = null);
}

/// <summary>
/// Represents an acquired critical region that must be disposed to release the lock.
/// </summary>
public interface ICriticalRegion : IDisposable
{
}
