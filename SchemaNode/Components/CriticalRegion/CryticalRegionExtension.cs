using SchemaNode.Context;

namespace SchemaNode.Components;

public static class CryticalRegionExtension
{
    /// <summary>
    /// Lock by key
    /// </summary>
    public static Task<ICriticalRegion> GetLockAsync(this SchemaContext context, string lockKey, params object[] args)
        => context.GetRequiredService<ICriticalRegionProvider>().AcquireAsync(string.Format(lockKey, args));

    /// <summary>
    /// Lock by key with timeout
    /// </summary>
    public static Task<ICriticalRegion> GetLockAsync(this SchemaContext context, string lockKey, TimeSpan timeout, params object[] args)
        => context.GetRequiredService<ICriticalRegionProvider>().AcquireAsync(string.Format(lockKey, args), timeout);
}
